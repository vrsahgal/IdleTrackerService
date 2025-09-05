public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
