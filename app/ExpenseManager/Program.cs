using ExpenseManager.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Register ExpenseService with DI
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// Register ChatService if using GenAI
builder.Services.AddScoped<IChatService, ChatService>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Expense Manager API",
        Version = "v1",
        Description = "REST API for the Expense Management System. All data operations go through these endpoints.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Expense Manager"
        }
    });
    // Include XML comments for better Swagger docs
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Add HttpClient for internal API calls from Razor Pages
builder.Services.AddHttpClient("ExpenseApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Always show Swagger (useful for testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Manager API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Expense Manager API";
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Map controllers (for API endpoints)
app.MapControllers();
// Map Razor Pages (for UI)
app.MapRazorPages();

// Redirect root to /Index
app.MapGet("/", () => Results.Redirect("/Index"));

app.Run();
