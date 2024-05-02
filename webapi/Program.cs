using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using static Microsoft.AspNetCore.Http.StatusCodes;

using webapi.Models;

var builder = WebApplication.CreateBuilder(args);

//Database setup
string ThatsTimeData = string.Empty;
string Accounts = string.Empty;

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
    ThatsTimeData = builder.Configuration.GetConnectionString("DataConnection");
    Accounts = builder.Configuration.GetConnectionString("IdentityConnection");
}
else
{
    ThatsTimeData = builder.Configuration.GetConnectionString("DataConnection");
    Accounts = builder.Configuration.GetConnectionString("IdentityConnection");
}

builder.Services.AddDbContext<IdentityContext>(opts =>
    opts.UseSqlServer(Accounts));

builder.Services.AddDbContext<DataContext>(opts =>
    opts.UseSqlServer(ThatsTimeData));

//User account configs
builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<IdentityContext>();
builder.Services.Configure<IdentityOptions>(opts =>
{
    opts.Password.RequiredLength = 6;
    opts.Password.RequireNonAlphanumeric = true;
    opts.Password.RequireLowercase = true;
    opts.Password.RequireUppercase = true;
    opts.Password.RequireDigit = true;
    opts.User.RequireUniqueEmail = true;
    opts.User.AllowedUserNameCharacters = "1234567890qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM_";
});


//Jwt configuration starts here
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey
            (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
});
//Jwt configuration ends here

builder.Services.AddAuthorization();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

builder.Services.AddControllers();

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = Status307TemporaryRedirect;
    options.HttpsPort = 5000;
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseStaticFiles();
app.UseDefaultFiles();

app.MapFallbackToFile("/index.html");

var scope = app.Services.CreateScope();
DataContext context = scope.ServiceProvider.GetRequiredService<DataContext>();
context.Database.Migrate();
scope.ServiceProvider.GetRequiredService<IdentityContext>().Database.Migrate();
context.createRoles(scope.ServiceProvider.GetRequiredService<DataContext>());

app.Run();
