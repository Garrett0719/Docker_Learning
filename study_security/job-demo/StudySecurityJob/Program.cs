Console.WriteLine(
    $"Job started: {DateTime.UtcNow:O}");

for (int i = 1; i <= 3; i++)
{
    Console.WriteLine($"Processing task {i}/3...");

    await Task.Delay(1000);
}

Console.WriteLine("All tasks completed.");

Console.WriteLine(
    $"Job finished: {DateTime.UtcNow:O}");
