
namespace helicon
{
	partial class ConfigForm
	{
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent()
		{
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label6 = new System.Windows.Forms.Label();
			this.txSqlServer = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.txSqlDatabase = new System.Windows.Forms.TextBox();
			this.txSqlUsername = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.txSqlPassword = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.txSmtpFromName = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.txSmtpPort = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.txSmtpServer = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.txSmtpFrom = new System.Windows.Forms.TextBox();
			this.txSmtpUser = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.txSmtpPass = new System.Windows.Forms.TextBox();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label6);
			this.groupBox2.Controls.Add(this.txSqlServer);
			this.groupBox2.Controls.Add(this.label8);
			this.groupBox2.Controls.Add(this.txSqlDatabase);
			this.groupBox2.Controls.Add(this.txSqlUsername);
			this.groupBox2.Controls.Add(this.label9);
			this.groupBox2.Controls.Add(this.label10);
			this.groupBox2.Controls.Add(this.txSqlPassword);
			this.groupBox2.Location = new System.Drawing.Point(12, 63);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(342, 138);
			this.groupBox2.TabIndex = 11;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "MSSQL Server Configuration";
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(18, 28);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(100, 23);
			this.label6.TabIndex = 0;
			this.label6.Text = "Server Address";
			// 
			// txSqlServer
			// 
			this.txSqlServer.Location = new System.Drawing.Point(124, 25);
			this.txSqlServer.Name = "txSqlServer";
			this.txSqlServer.Size = new System.Drawing.Size(201, 20);
			this.txSqlServer.TabIndex = 1;
			// 
			// label8
			// 
			this.label8.Location = new System.Drawing.Point(18, 54);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(100, 23);
			this.label8.TabIndex = 2;
			this.label8.Text = "Username";
			// 
			// txSqlDatabase
			// 
			this.txSqlDatabase.Location = new System.Drawing.Point(124, 103);
			this.txSqlDatabase.Name = "txSqlDatabase";
			this.txSqlDatabase.Size = new System.Drawing.Size(201, 20);
			this.txSqlDatabase.TabIndex = 4;
			// 
			// txSqlUsername
			// 
			this.txSqlUsername.Location = new System.Drawing.Point(124, 51);
			this.txSqlUsername.Name = "txSqlUsername";
			this.txSqlUsername.Size = new System.Drawing.Size(201, 20);
			this.txSqlUsername.TabIndex = 2;
			// 
			// label9
			// 
			this.label9.Location = new System.Drawing.Point(18, 106);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(100, 23);
			this.label9.TabIndex = 6;
			this.label9.Text = "Database Name";
			// 
			// label10
			// 
			this.label10.Location = new System.Drawing.Point(18, 80);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(100, 23);
			this.label10.TabIndex = 4;
			this.label10.Text = "Password";
			// 
			// txSqlPassword
			// 
			this.txSqlPassword.Location = new System.Drawing.Point(124, 77);
			this.txSqlPassword.Name = "txSqlPassword";
			this.txSqlPassword.Size = new System.Drawing.Size(201, 20);
			this.txSqlPassword.TabIndex = 3;
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(12, 12);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(342, 28);
			this.label7.TabIndex = 12;
			this.label7.Text = "The tool requires connection to an SQL and an SMTP Server, please specify the det" +
			"ails of your connections below.";
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(178, 391);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(85, 23);
			this.button1.TabIndex = 11;
			this.button1.Text = "Save";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.Button1Click);
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point(269, 391);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(85, 23);
			this.button2.TabIndex = 12;
			this.button2.Text = "Close";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(this.Button2Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.txSmtpFromName);
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.txSmtpPort);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.txSmtpServer);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.txSmtpFrom);
			this.groupBox1.Controls.Add(this.txSmtpUser);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.txSmtpPass);
			this.groupBox1.Location = new System.Drawing.Point(12, 216);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(342, 164);
			this.groupBox1.TabIndex = 11;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "SMTP Server Configuration";
			// 
			// txSmtpFromName
			// 
			this.txSmtpFromName.Location = new System.Drawing.Point(124, 129);
			this.txSmtpFromName.Name = "txSmtpFromName";
			this.txSmtpFromName.Size = new System.Drawing.Size(201, 20);
			this.txSmtpFromName.TabIndex = 10;
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(18, 132);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(100, 23);
			this.label5.TabIndex = 8;
			this.label5.Text = "From (Name)";
			// 
			// txSmtpPort
			// 
			this.txSmtpPort.Location = new System.Drawing.Point(286, 25);
			this.txSmtpPort.Name = "txSmtpPort";
			this.txSmtpPort.Size = new System.Drawing.Size(39, 20);
			this.txSmtpPort.TabIndex = 6;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(18, 28);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(100, 23);
			this.label1.TabIndex = 0;
			this.label1.Text = "Server and Port";
			// 
			// txSmtpServer
			// 
			this.txSmtpServer.Location = new System.Drawing.Point(124, 25);
			this.txSmtpServer.Name = "txSmtpServer";
			this.txSmtpServer.Size = new System.Drawing.Size(156, 20);
			this.txSmtpServer.TabIndex = 5;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(18, 54);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 23);
			this.label2.TabIndex = 2;
			this.label2.Text = "Username";
			// 
			// txSmtpFrom
			// 
			this.txSmtpFrom.Location = new System.Drawing.Point(124, 103);
			this.txSmtpFrom.Name = "txSmtpFrom";
			this.txSmtpFrom.Size = new System.Drawing.Size(201, 20);
			this.txSmtpFrom.TabIndex = 9;
			// 
			// txSmtpUser
			// 
			this.txSmtpUser.Location = new System.Drawing.Point(124, 51);
			this.txSmtpUser.Name = "txSmtpUser";
			this.txSmtpUser.Size = new System.Drawing.Size(201, 20);
			this.txSmtpUser.TabIndex = 7;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(18, 106);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(100, 23);
			this.label3.TabIndex = 6;
			this.label3.Text = "From (Email)";
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(18, 80);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(100, 23);
			this.label4.TabIndex = 4;
			this.label4.Text = "Password";
			// 
			// txSmtpPass
			// 
			this.txSmtpPass.Location = new System.Drawing.Point(124, 77);
			this.txSmtpPass.Name = "txSmtpPass";
			this.txSmtpPass.Size = new System.Drawing.Size(201, 20);
			this.txSmtpPass.TabIndex = 8;
			// 
			// ConfigForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(366, 421);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.groupBox2);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "ConfigForm";
			this.Text = "Helicon Configuration";
			this.Load += new System.EventHandler(this.ConfigFormLoad);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);
		}
		private System.Windows.Forms.TextBox txSmtpFromName;
		private System.Windows.Forms.TextBox txSmtpPass;
		private System.Windows.Forms.TextBox txSmtpUser;
		private System.Windows.Forms.TextBox txSmtpFrom;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.TextBox txSmtpPort;
		private System.Windows.Forms.TextBox txSmtpServer;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.TextBox txSqlServer;
		private System.Windows.Forms.TextBox txSqlDatabase;
		private System.Windows.Forms.TextBox txSqlUsername;
		private System.Windows.Forms.TextBox txSqlPassword;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.GroupBox groupBox2;
	}
}
