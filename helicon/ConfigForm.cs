
using System;
using System.Drawing;
using System.Windows.Forms;
using IronRockUtils;

namespace helicon
{
	public partial class ConfigForm : Form
	{
		Config config;

		public ConfigForm(Config config)
		{
			this.config = config;
			InitializeComponent();
		}

		void ConfigFormLoad(object sender, EventArgs e)
		{
			txSqlServer.Text = config.get("sqlServer");
			txSqlUsername.Text = config.get("sqlUsername");
			txSqlPassword.Text = config.get("sqlPassword");
			txSqlDatabase.Text = config.get("sqlDatabase");

			txSmtpServer.Text = config.get("smtpHost");
			txSmtpPort.Text = config.get("smtpPort");
			txSmtpUser.Text = config.get("smtpUser");
			txSmtpPass.Text = config.get("smtpPass");
			txSmtpFrom.Text = config.get("smtpFrom");
			txSmtpFromName.Text = config.get("smtpFromName");
		}

		void Button1Click(object sender, EventArgs e)
		{
			config.put("sqlServer", txSqlServer.Text);
			config.put("sqlUsername", txSqlUsername.Text);
			config.put("sqlPassword", txSqlPassword.Text);
			config.put("sqlDatabase", txSqlDatabase.Text);

			config.put("smtpHost", txSmtpServer.Text);
			config.put("smtpPort", txSmtpPort.Text);
			config.put("smtpUser", txSmtpUser.Text);
			config.put("smtpPass", txSmtpPass.Text);
			config.put("smtpFrom", txSmtpFrom.Text);
			config.put("smtpFromName", txSmtpFromName.Text);

			if (!config.save()) {
				MessageBox.Show("Unable to save configuration to the registry.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Error);
			} else {
				MessageBox.Show("Configuration has been saved successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		void Button2Click(object sender, EventArgs e)
		{
			this.Close();
		}
	}
};
