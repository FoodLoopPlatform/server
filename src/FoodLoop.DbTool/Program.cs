using FoodLoop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FoodLoop.DbTool;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Resolve path to appsettings.json in FoodLoop.API
        var baseDir = AppContext.BaseDirectory;
        var apiPath = Path.GetFullPath(Path.Combine(baseDir, "../../../src/FoodLoop.API"));
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.GetFullPath(Path.Combine(baseDir, "../../../../src/FoodLoop.API"));
        }
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/FoodLoop.API"));
        }
        if (!Directory.Exists(apiPath))
        {
            apiPath = Directory.GetCurrentDirectory();
        }

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var connStr = configuration.GetConnectionString("DefaultConnection") 
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=db60802.public.databaseasp.net; Database=db60802; User Id=db60802; Password=Kq2@6?eBC7!o; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connStr);

        using var db = new ApplicationDbContext(optionsBuilder.Options);

        if (args.Contains("--reset") || args.Contains("-r"))
        {
            await DataCleaner.ResetDatabaseAsync(db);
            return 0;
        }
        else if (args.Contains("--seed") || args.Contains("-s"))
        {
            await DataCleaner.ResetDatabaseAsync(db);
            await DataSeeder.SeedLargeDatasetAsync(db);
            return 0;
        }
        else
        {
            Console.WriteLine("FoodLoop Database Management Tool");
            Console.WriteLine("Usage:");
            Console.WriteLine("  --reset, -r   Wipe and clean all tables");
            Console.WriteLine("  --seed,  -s   Reset and populate all tables with a large-scale test dataset");
            return 1;
        }
    }
}
