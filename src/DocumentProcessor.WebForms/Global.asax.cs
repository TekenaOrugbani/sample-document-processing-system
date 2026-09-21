using System;
using System.Diagnostics;
using System.Net;
using System.Web;
using System.Web.Hosting;
using DocumentProcessor.WebForms.Data;

namespace DocumentProcessor.WebForms
{
    public class Global : HttpApplication
    {
        /// <summary>Application state key holding the resolved <see cref="DatabaseInfo"/>.</summary>
        public const string DatabaseInfoKey = "DatabaseInfo";

        protected void Application_Start(object sender, EventArgs e)
        {
            // Trace output has nowhere to go under IIS Express, so send it to a file next to
            // the uploads. A production app would reach for log4net or ELMAH instead.
            Trace.Listeners.Add(new TextWriterTraceListener(HostingEnvironment.MapPath("~/App_Data/trace.log")));
            Trace.AutoFlush = true;

            // .NET Framework 4.7 and later pick up the operating system default, but plenty
            // of AWS endpoints refuse anything below TLS 1.2 and this app has been moved
            // between machines, so pin it explicitly.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var connection = DatabaseConnectionResolver.Resolve();

            if (connection.Warning != null)
            {
                Trace.TraceWarning(connection.Warning);
            }

            DocumentDbContext.ConnectionString = connection.ConnectionString;
            Application[DatabaseInfoKey] = connection.Info;

            try
            {
                DatabaseInitializer.EnsureSchema(connection.ConnectionString);
            }
            catch (Exception ex)
            {
                // Do not take the whole application down: the pages already report a
                // failure to read documents, and this way the error is visible in one place.
                Trace.TraceError("Could not prepare the database. {0}", ex);
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();

            if (exception != null)
            {
                Trace.TraceError("Unhandled application error. {0}", exception);
            }
        }
    }
}
