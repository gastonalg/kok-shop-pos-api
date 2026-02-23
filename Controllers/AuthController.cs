var secret = Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrEmpty(secret))
{
    return BadRequest("JWT_SECRET no existe en runtime");
}
