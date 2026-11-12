using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Clipsa;

public static class UpdateInstaller
{
    private sealed record Plan(int ParentId, long ParentStart, string Target, string Package, bool Portable, string Manifest, string? DataOverride);
    public static void Start(ReleaseInfo release, string package)
    {
        if (!release.SignatureVerified || release.SignedManifest is null) throw new InvalidDataException("Actualización sin firma de confianza.");
        var root = AppPaths.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var cache = Path.GetDirectoryName(Path.GetFullPath(package))!;
        var attempt = Path.Combine(cache, "apply-" + Guid.NewGuid().ToString("N"));
        var helper = Path.Combine(attempt, "worker"); Directory.CreateDirectory(helper);
        var inventoryPath = Path.Combine(root, "app-files.json");
        var inventory = JsonSerializer.Deserialize<string[]>(File.ReadAllText(inventoryPath)) ?? throw new InvalidDataException("Falta el inventario de la aplicación.");
        foreach (var relative in inventory.Concat(new[] { "app-files.json" }).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var source = SafePath(root, relative); var destination = SafePath(helper, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, false);
        }
        if (AppPaths.IsPortable) File.WriteAllText(Path.Combine(helper, "portable.mode"), "");
        using var parent = Process.GetCurrentProcess();
        var plan = new Plan(parent.Id, parent.StartTime.ToUniversalTime().Ticks, root, Path.GetFullPath(package), AppPaths.IsPortable, release.SignedManifest, Environment.GetEnvironmentVariable("CLIPSA_DATA_DIR"));
        var path = Path.Combine(attempt, "apply-plan.json"); File.WriteAllText(path, JsonSerializer.Serialize(plan));
        var start = new ProcessStartInfo(Path.Combine(helper, "Clipsa.exe")) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(path);
        start.Environment["CLIPSA_DATA_DIR"] = Path.Combine(attempt, "worker-data");
        _ = Process.Start(start) ?? throw new IOException("No se pudo iniciar el actualizador.");
    }
    public static int RunWorker(string planPath)
    {
        Plan? plan = null;
        try
        {
            plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(planPath)) ?? throw new InvalidDataException("Plan vacío.");
            var release = UpdateService.ParseManifest(plan.Manifest, plan.Portable);
            if (!release.SignatureVerified) throw new InvalidDataException("Firma inválida.");
            VerifyHash(plan.Package, release.Sha256);
            try
            {
                using var parent = Process.GetProcessById(plan.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStart || !Path.GetFullPath(Path.GetDirectoryName(parent.MainModule!.FileName)!).Equals(Path.GetFullPath(plan.Target), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La aplicación de destino no coincide.");
                if (!parent.WaitForExit(120_000)) throw new IOException("Clipsa sigue abierta. Guarda tus capturas y ciérrala antes de actualizar.");
            }
            catch (ArgumentException) { /* parent already exited */ }
            using (var updateLock = new Mutex(false, SingleInstance.MutexName))
            {
                bool locked;
                try { locked = updateLock.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
                if (!locked) throw new IOException("Hay otra copia de Clipsa abierta. La actualización no se aplicará.");
                try
                {
                    if (plan.Portable) ApplyPortable(plan.Package, plan.Target, Path.Combine(Path.GetDirectoryName(planPath)!, "transaction"));
                }
                finally { updateLock.ReleaseMutex(); }
            } // Close the HANDLE as well as releasing ownership before launching Setup.
            if (!plan.Portable)
            {
                var setup = new ProcessStartInfo(plan.Package) { UseShellExecute = false };
                foreach (var argument in new[] { "/SILENT", "/NORESTART", "/SP-", "/DIR=" + plan.Target }) setup.ArgumentList.Add(argument);
                using var process = Process.Start(setup) ?? throw new IOException("No se pudo abrir el instalador."); process.WaitForExit();
                if (process.ExitCode is not (0 or 3010)) throw new IOException("El instalador terminó con código " + process.ExitCode + ". No se han eliminado tus datos.");
            }
            Restart(plan);
            return 0;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Aplicar actualización");
            System.Windows.MessageBox.Show("No se pudo aplicar la actualización: " + ex.Message + "\nTus archivos personales se conservan. Puedes descargar la versión desde GitHub.", "Clipsa", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            if (plan is not null && File.Exists(Path.Combine(plan.Target, "Clipsa.exe")))
                try { Restart(plan); } catch { }
            return 1;
        }
    }
    private static void Restart(Plan plan)
    {
        var start = new ProcessStartInfo(Path.Combine(plan.Target, "Clipsa.exe")) { UseShellExecute = false };
        if (string.IsNullOrWhiteSpace(plan.DataOverride)) start.Environment.Remove("CLIPSA_DATA_DIR");
        else start.Environment["CLIPSA_DATA_DIR"] = plan.DataOverride;
        Process.Start(start);
    }
    public static void VerifyHash(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA256 del paquete incorrecto.");
    }
    public static string SafePath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Ruta de paquete inválida.");
        var parts = relative.Replace('\\', '/').Split('/');
        if (parts.Any(p => p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')) || parts[0].Equals("data", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("El paquete intenta modificar datos personales o salir de su carpeta.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera de la carpeta de la aplicación.");
        for (var folder = Path.GetDirectoryName(path); folder is not null && folder.Length >= fullRoot.Length; folder = Path.GetDirectoryName(folder))
            if (Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("No se actualizan carpetas con enlaces o redirecciones.");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Archivo de destino redirigido.");
        return path;
    }
    public static void ApplyPortable(string package, string target, string transaction)
    {
        var targetRoot = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (targetRoot.Equals(Path.GetPathRoot(targetRoot)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Mueve la portable a una subcarpeta antes de actualizar.");
        var payload = Path.Combine(transaction, "payload"); var backup = Path.Combine(transaction, "backup");
        if (Directory.Exists(transaction)) throw new IOException("La transacción de actualización ya existe.");
        Directory.CreateDirectory(payload); Directory.CreateDirectory(backup);
        using (var zip = ZipFile.OpenRead(package))
        {
            if (zip.Entries.Count > 10_000 || zip.Entries.Sum(e => e.Length) > 1_000_000_000) throw new InvalidDataException("Paquete demasiado grande.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !names.Add(entry.FullName.Replace('\\', '/'))) throw new InvalidDataException("Enlace o archivo duplicado en el paquete.");
                var path = SafePath(payload, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path, false);
            }
        }
        foreach (var required in new[] { "Clipsa.exe", "Clipsa.dll", "coreclr.dll", "Clipsa.runtimeconfig.json", "portable.mode", "app-files.json" })
            if (!File.Exists(Path.Combine(payload, required))) throw new InvalidDataException("Portable incompleta: " + required);
        var files = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(payload, "app-files.json"))) ?? throw new InvalidDataException("Inventario inválido.");
        var completed = new List<(string Destination, string? Backup)>();
        try
        {
            foreach (var relative in files.Concat(new[] { "app-files.json", "portable.mode" }).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var source = SafePath(payload, relative); var destination = SafePath(targetRoot, relative);
                if (!File.Exists(source)) throw new InvalidDataException("Falta un archivo del inventario.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string? saved = null;
                if (File.Exists(destination)) { saved = SafePath(backup, relative); Directory.CreateDirectory(Path.GetDirectoryName(saved)!); File.Copy(destination, saved, false); }
                var temp = destination + "." + Guid.NewGuid().ToString("N") + ".update";
                try { File.Copy(source, temp, false); File.Move(temp, destination, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
                completed.Add((destination, saved));
            }
        }
        catch
        {
            foreach (var file in completed.AsEnumerable().Reverse())
                if (file.Backup is not null) File.Copy(file.Backup, file.Destination, true); else File.Delete(file.Destination);
            throw;
        }
        // Never recursively delete the target: captures, data, custom folders and old user files stay intact.
    }
}
