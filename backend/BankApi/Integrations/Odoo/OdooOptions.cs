namespace BankApi.Integrations.Odoo;

public class OdooOptions
{
    public const string SectionName = "Odoo";

    public string Url { get; set; } = "http://odoo:8069";
    public string Database { get; set; } = "bankportal";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin";
}
