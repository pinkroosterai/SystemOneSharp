namespace SystemOneSharp.Testing;

/// <summary>Console assertion helpers matching the core verification harness: print PASS or throw.</summary>
public static class Verify
{
    public static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception($"Verification failed: {name}");
        Console.WriteLine($"PASS {name}");
    }

    public static T Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T error) { Console.WriteLine($"PASS {name}"); return error; }
        throw new Exception($"Verification failed: {name} (expected {typeof(T).Name}).");
    }

    public static async Task<T> ThrowsAsync<T>(Func<Task> action, string name) where T : Exception
    {
        try { await action(); }
        catch (T error) { Console.WriteLine($"PASS {name}"); return error; }
        throw new Exception($"Verification failed: {name} (expected {typeof(T).Name}).");
    }
}
