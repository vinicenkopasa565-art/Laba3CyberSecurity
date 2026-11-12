using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

string connString = $"Host=db;Port=5432;Username={dbUser};Password={dbPass};Database={dbName}";

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/data", async () =>
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using (var cmd = new NpgsqlCommand(@"
        CREATE TABLE IF NOT EXISTS announcements (
            id BIGSERIAL PRIMARY KEY,
            title VARCHAR(120) NOT NULL,
            body TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        
        INSERT INTO announcements (title, body) 
        SELECT 'Первий запис', 'Текст першого тестового запису'
        WHERE NOT EXISTS (SELECT 1 FROM announcements WHERE title = 'Первий запис');
        
        INSERT INTO announcements (title, body) 
        SELECT 'Другий запис', 'Текст другого тестового запису'
        WHERE NOT EXISTS (SELECT 1 FROM announcements WHERE title = 'Другий запис');
    ", conn))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    var results = new List<object>();
    await using (var cmd = new NpgsqlCommand("SELECT * FROM announcements", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Body = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3)
            });
        }
    }
    return results;
});

app.MapPost("/api/data", async (NewAnnouncement newRecord) =>
{
    if (string.IsNullOrWhiteSpace(newRecord.Title) || string.IsNullOrWhiteSpace(newRecord.Body))
    {
        return Results.BadRequest(new { error = "Помилка: Заголовок та текст обов'язкові для заповнення." });
    }

    if (newRecord.Title.Length > 120)
    {
        return Results.BadRequest(new { error = "Помилка: Заголовок не може перевищувати 120 символів." });
    }

    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");
    string connString = $"Host=db;Port=5432;Username={dbUser};Password={dbPass};Database={dbName}";

    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(
        "INSERT INTO announcements (title, body) VALUES (@titleParam, @bodyParam) RETURNING id;", conn);

    cmd.Parameters.AddWithValue("titleParam", newRecord.Title);
    cmd.Parameters.AddWithValue("bodyParam", newRecord.Body);

    var newId = (long)await cmd.ExecuteScalarAsync();

    return Results.Ok(new { message = "Запис успішно створено!", id = newId });
});

app.Run();

public class NewAnnouncement
{
    public string? Title { get; set; }
    public string? Body { get; set; }
}