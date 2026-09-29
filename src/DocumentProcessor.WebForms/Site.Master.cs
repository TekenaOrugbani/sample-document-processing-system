using System;
using System.Web;
using System.Web.UI;
using DocumentProcessor.WebForms.Data;

namespace DocumentProcessor.WebForms
{
    public partial class SiteMaster : MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var info = HttpContext.Current.Application[Global.DatabaseInfoKey] as DatabaseInfo;

            if (info == null)
            {
                DatabasePillText.Text = "Database not resolved";
                CredentialSourceText.Text = "unavailable";
                ProviderNameText.Text = "no database";
                return;
            }

            var isPostgres = info.Provider == DatabaseProvider.PostgreSql;

            DatabasePill.Attributes["class"] = isPostgres ? "db-pill db-pill-postgres" : "db-pill";
            DatabasePillIcon.Attributes["class"] = isPostgres ? "bi bi-rocket-takeoff-fill" : "bi bi-database";

            DatabasePillText.Text = Server.HtmlEncode(info.DisplayName + " · " + info.HostAddress);
            CredentialSourceText.Text = Server.HtmlEncode(info.CredentialSource);
            ProviderNameText.Text = Server.HtmlEncode(info.DisplayName);
        }
    }
}
