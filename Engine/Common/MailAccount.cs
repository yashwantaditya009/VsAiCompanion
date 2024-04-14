﻿using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public partial class MailAccount : SettingsListFileItem
	{
		public MailAccount()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>Email Name</summary>
		[DefaultValue("")]
		public string EmailName { get => _EmailName; set => SetProperty(ref _EmailName, value); }
		string _EmailName;

		/// <summary>Email Address</summary>
		[DefaultValue("")]
		public string EmailAddress { get => _EmailAddress; set => SetProperty(ref _EmailAddress, value); }
		string _EmailAddress;

		/// <summary>Server Name</summary>
		[DefaultValue("")]
		public string ServerHost { get => _ServerHost; set => SetProperty(ref _ServerHost, value); }
		string _ServerHost;

		/// <summary>Server Port</summary>
		[DefaultValue(993)]
		public int ServerImapPort { get => _ServerImapPort; set => SetProperty(ref _ServerImapPort, value); }
		int _ServerImapPort;

		/// <summary>IMAP Connection Security</summary>
		[DefaultValue(MailConnectionSecurity.SslOnConnect)]
		public MailConnectionSecurity ImapConnectionSecurity { get => _ImapConnectionSecurity; set => SetProperty(ref _ImapConnectionSecurity, value); }
		MailConnectionSecurity _ImapConnectionSecurity;

		/// <summary>Server Port</summary>
		[DefaultValue(465)]
		public int ServerSmtpPort { get => _ServerSmtpPort; set => SetProperty(ref _ServerSmtpPort, value); }
		int _ServerSmtpPort;

		/// <summary>SMTP Connection Security</summary>
		[DefaultValue(MailConnectionSecurity.SslOnConnect)]
		public MailConnectionSecurity SmtpConnectionSecurity { get => _SmtpConnectionSecurity; set => SetProperty(ref _SmtpConnectionSecurity, value); }
		MailConnectionSecurity _SmtpConnectionSecurity;


		/// <summary>Username</summary>
		[DefaultValue("")]
		public string Username { get => _Username; set => SetProperty(ref _Username, value); }
		string _Username;

		/// <summary>Organization key. Usage from these API requests will count against the specified organization's subscription quota.</summary>
		[XmlIgnore, JsonIgnore]
		public string Password
		{
			get => UserDecrypt(_PasswordEncrypted);
			set { _PasswordEncrypted = UserEncrypt(value); OnPropertyChanged(); }
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(Password))]
		public string _PasswordEncrypted { get; set; }

		#region Communication Security

		/// <summary>
		/// Emails that are allowed to communicate with the AI on this account.
		/// </summary>
		[DefaultValue("")]
		public string AllowedSenders { get => _AllowedSenders; set => SetProperty(ref _AllowedSenders, value); }
		string _AllowedSenders;

		/// <summary>
		/// Emails that are allowed to receive replies from this account.
		/// </summary>
		[DefaultValue("")]
		public string AllowedRecipients { get => _AllowedRecipients; set => SetProperty(ref _AllowedRecipients, value); }
		string _AllowedRecipients;

		/// <summary>
		/// Enable Allowed Senders filter.
		/// </summary>
		[DefaultValue(true)]
		public bool CheckSenders { get => _CheckSenders; set => SetProperty(ref _CheckSenders, value); }
		bool _CheckSenders;

		/// <summary>
		/// Enable Allowed Recipients filter.
		/// </summary>
		[DefaultValue(true)]
		public bool CheckRecipients { get => _CheckRecipients; set => SetProperty(ref _CheckRecipients, value); }
		bool _CheckRecipients;

		/// <summary>
		/// Require valid digital signature.
		/// </summary>
		[DefaultValue(true)]
		public bool CheckDigitalSignature { get => _CheckDigitalSignature; set => SetProperty(ref _CheckDigitalSignature, value); }
		bool _CheckDigitalSignature;

		/// <summary>
		/// Require to pass DKIM validation.
		/// </summary>
		[DefaultValue(true)]
		public bool CheckDkim { get => _CheckDkim; set => SetProperty(ref _CheckDkim, value); }
		bool _CheckDkim;


		#endregion

		/// <summary>
		/// Inbox monitoring is enabled.
		/// </summary>
		[DefaultValue(false)]
		public bool MonitorInbox { get => _MonitorInbox; set => SetProperty(ref _MonitorInbox, value); }
		bool _MonitorInbox;

		#region Encrypt Settings 

		internal static string UserEncrypt(string text)
		{
			try
			{
				if (string.IsNullOrEmpty(text))
					return null;
				//var user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
				var user = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Encrypt(text, user);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		internal static string UserDecrypt(string base64)
		{
			try
			{
				if (string.IsNullOrEmpty(base64))
					return null;
				//var user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
				var user = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Decrypt(base64, user);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		#endregion

	}
}
