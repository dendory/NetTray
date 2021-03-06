//
// NetTray - (C) 2016 Patrick Lambert - http://dendory.net - Provided under the MIT License
//

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using Microsoft.Win32;

[assembly: AssemblyTitle("NetTray")]
[assembly: AssemblyCopyright("(C) 2016 Patrick Lambert")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace NetTrayNS
{
	public class NetTray : Form
	{
		private NotifyIcon tray_icon; // The tray icon itself
		private ContextMenu tray_menu; // The right click menu
		private int interval = 1; // Interval used to check public IP and latency
		private string url = "www.google.com"; // URL to ping for latency
		private int min_latency = 500; // Minimum latency (ms) until slow connection is reported
		private int dis_len = 10; // How long (secs) to display info bubbles
		private string cur_ips = ""; // Current public and private IPs
		private string new_ips = ""; // Latest public and private IPs
		private int con_state = 0; // Connection state (0 = all fine, 1 = slow latency, 2 = no reply)
		private long cur_latency = -1; // Current latency (ms)
		private RegistryKey rkey; // Registry key to read config values
		private string log_file = Path.GetTempPath() + "nettray.log"; // Log file
		private int show_startup = 1; // Show the startup IP information
		private int log_latency = 0; // Write every ping to the log
		private int con_threas = 0; // Counter for 3 pings = no connection
		private StreamWriter logfile; // Log file holder
		private string lookup_name = ""; // Buffer for lookup value 
		private string url_name = "http://"; // Buffer for url value 

		[DllImport("kernel32")]
		extern static UInt64 GetTickCount64();

		[STAThread]
		static void Main(string[] args)
		{
			Application.Run(new NetTray());
		}

		public NetTray()
		{
			rkey = Registry.CurrentUser.OpenSubKey("Software\\NetTray"); // Location of config values
			if(rkey == null) // Location does not exist, create config values
			{
				rkey = Registry.CurrentUser.CreateSubKey("Software\\NetTray");
				rkey.SetValue("latency_check_url", url);
				rkey.SetValue("check_interval_in_seconds", interval);
				rkey.SetValue("minimal_latency_in_ms", min_latency);
				rkey.SetValue("info_display_length_in_seconds", dis_len);
				rkey.SetValue("log_file", log_file);
				rkey.SetValue("show_startup_info", show_startup);
				rkey.SetValue("write_latency_to_log", log_latency);
			}
			else // Try to load config from registry
			{
				try
				{
					log_file = (string)rkey.GetValue("log_file");
					url = (string)rkey.GetValue("latency_check_url");
					interval = (int)rkey.GetValue("check_interval_in_seconds");
					min_latency = (int)rkey.GetValue("minimal_latency_in_ms");
					dis_len = (int)rkey.GetValue("info_display_length_in_seconds");
					show_startup = (int)rkey.GetValue("show_startup_info");
					log_latency = (int)rkey.GetValue("write_latency_to_log");
				}
				catch(Exception) // Display message but keep going
				{
					MessageBox.Show("An error occured while loading Registy settings. Using default values.", "NetTray");
				}
			}
			logfile = File.AppendText(log_file);
			logfile.AutoFlush = true;
			logfile.WriteLine(DateTime.Now + " - Starting up.");
			tray_menu = new ContextMenu(); // Make tray menu
			tray_menu.MenuItems.Add("Interfaces", interfaces);
			tray_menu.MenuItems.Add("Latency", latency);
			tray_menu.MenuItems.Add("Lookup", nslookup);
			tray_menu.MenuItems.Add("Test website", testwebsite);
			tray_menu.MenuItems.Add("Uptime", uptime);
			tray_menu.MenuItems.Add("Refresh", refresh);
			tray_menu.MenuItems.Add("View log", viewlog);
			tray_menu.MenuItems.Add("About", about);
			tray_menu.MenuItems.Add("-");
			tray_menu.MenuItems.Add("Exit", exit);
			tray_icon = new NotifyIcon(); // Make tray icon
			tray_icon.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("res.notify_icon"));
			tray_icon.ContextMenu = tray_menu;
			tray_icon.Visible = true;
			new_ips = "Private IP: " + get_private_ip() + "\r\nPublic IP: " + get_public_ip(); // Get private and public IPs
			tray_icon.Text = new_ips;
			logfile.WriteLine(DateTime.Now + " - IP information:\r\n" + new_ips);
			if(show_startup == 1)
			{
				tray_icon.BalloonTipTitle = "NetTray"; // Show initial IPs info bubble
				tray_icon.BalloonTipText = new_ips;
				tray_icon.ShowBalloonTip(dis_len * 1000);
			}
			cur_ips = new_ips;
			BackgroundWorker bw = new BackgroundWorker(); // Thread to check latency and IPs
			bw.WorkerReportsProgress = true;
			bw.DoWork += new DoWorkEventHandler(delegate(object o, DoWorkEventArgs args) // Enter thread here
			{
				BackgroundWorker b = o as BackgroundWorker;
				Ping p = new Ping();
				while(true) // Loop forever
				{
					PingReply r = p.Send(url);
					if(r.Status == IPStatus.Success) // We got a ping reply
					{
						con_threas = 0;
						if(r.RoundtripTime > min_latency) // Latency is above minimum acceptable value
						{
							b.ReportProgress(1, "Slow network connection detected: " + r.RoundtripTime.ToString() + "ms.");
						}
						else // Latency is fine
						{
							b.ReportProgress(0, "Connection restored.");
						}
						cur_latency = r.RoundtripTime; // Store latency for later use
						if(log_latency == 1) logfile.WriteLine(DateTime.Now + " - Latency: " + cur_latency.ToString() + " ms.");
					}
					else // Timed out on the ping
					{
						con_threas = con_threas + 1;
						if(log_latency == 1) logfile.WriteLine(DateTime.Now + " - Latency: Timed out.");
						if(con_threas > 2)
						{
							b.ReportProgress(2, "No network connection detected.");
							con_threas = 0;
						}
					}
					new_ips = "Private IP: " + get_private_ip() + "\r\nPublic IP: " + get_public_ip(); // Get new IPs
					tray_icon.Text = new_ips;
					if(string.Compare(new_ips, cur_ips) != 0) // Check if new IPs are diff from old values
					{
						logfile.WriteLine(DateTime.Now + " - IP information:\r\n" + new_ips);
						tray_icon.BalloonTipTitle = "NetTray";
						tray_icon.BalloonTipText = new_ips;
						tray_icon.ShowBalloonTip(dis_len * 1000);				
					}
					cur_ips = new_ips;
					Thread.Sleep(interval * 1000); // Sleep this thread for interval time
				}
	        });
			bw.ProgressChanged += new ProgressChangedEventHandler(delegate(object o, ProgressChangedEventArgs args) // Out of thread here
			{
				if(con_state != args.ProgressPercentage) // Check if state changed, if so show bubble with message from thread
				{
					logfile.WriteLine(DateTime.Now + " - " + args.UserState as String); 
					tray_icon.BalloonTipTitle = "NetTray";
					tray_icon.BalloonTipText = args.UserState as String;
					tray_icon.ShowBalloonTip(dis_len * 1000);
				}
				con_state = args.ProgressPercentage;
			});
			bw.RunWorkerAsync(); // Start thread
		}

		public static string input(string title, string text, string value) // Show a prompt on the screen for user input
		{
			Form prompt = new Form()
			{
				Width = 300,
				Height = 150,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				Text = title,
				StartPosition = FormStartPosition.CenterScreen
			};
			Label textLabel = new Label() { Left = 50, Top=20, Width=200, Text=text };
			TextBox textBox = new TextBox() { Left = 50, Top=50, Width=200, Text=value };
			Button confirmation = new Button() { Text = "Ok", Left=150, Width=100, Top=80, DialogResult = DialogResult.OK };
			confirmation.Click += (sender, e) => { prompt.Close(); };
			prompt.Controls.Add(textBox);
			prompt.Controls.Add(confirmation);
			prompt.Controls.Add(textLabel);
			prompt.AcceptButton = confirmation;
 			return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
		}

		private void exit(object sender, EventArgs e) // Clicked Exit
		{
			logfile.WriteLine(DateTime.Now + " - Shutting down.");
			logfile.Close();
			Application.Exit();
		}

		private void viewlog(object sender, EventArgs e) // Clicked View log
		{
			Process.Start(log_file);
		}

		private void nslookup(object sender, EventArgs e) // Clicked Lookup
		{
			string result = "Unknown";
			lookup_name = input("Lookup", "Enter a hostname or IP address:", lookup_name);
			try // Try to resolve hostname from ip
			{
				IPAddress hostIPAddress = IPAddress.Parse(lookup_name);
				IPHostEntry hostInfo = Dns.GetHostEntry(hostIPAddress);
				result = hostInfo.HostName;
			}
			catch (Exception)
			{
				try // If it didn't work, we probably need to resolve ip from hostname
				{
					IPHostEntry hostEntry = Dns.GetHostEntry(lookup_name);
					var ip = hostEntry.AddressList[0];
					result = ip.ToString();
				}
				catch(Exception){} // Not a valid hostname or ip
			}
			MessageBox.Show(lookup_name + " = " + result, "Lookup");
		}

		private void testwebsite(object sender, EventArgs e) // Clicked Test website
		{
			HttpWebResponse result;
			url_name = input("Test website", "Enter a valid URL:", url_name);
			try // Try to connect
			{
				HttpWebRequest req = WebRequest.Create(url_name) as HttpWebRequest;
				result = req.GetResponse() as HttpWebResponse;
			}
			catch (WebException ex)
			{
				result = ex.Response as HttpWebResponse;
			}
			catch (Exception) // Crazy how many different ways it can fail
			{
				MessageBox.Show("Could not connect to " + url_name + ".", "Test website");
				return;
			}
			if(result != null)
			{
				MessageBox.Show("Status of " + url_name + ": " + result.StatusCode, "Test website");
			}
			else
			{
				MessageBox.Show("Could not connect to " + url_name + ".", "Test website");
			}
		}

		private void refresh(object sender, EventArgs e) // Clicked Refresh
		{
			new_ips = "Private IP: " + get_private_ip() + "\r\nPublic IP: " + get_public_ip(); // Get new IPs
			tray_icon.Text = new_ips;
			if(string.Compare(new_ips, cur_ips) != 0) // Check if new IPs are diff from old values
			{
				logfile.WriteLine(DateTime.Now + " - IP information:\r\n" + new_ips);
				tray_icon.BalloonTipTitle = "NetTray";
				tray_icon.BalloonTipText = new_ips;
				tray_icon.ShowBalloonTip(dis_len * 1000);
			}
			cur_ips = new_ips;
		}

		private void uptime(object sender, EventArgs e) // Clicked Uptime
		{
			var uptime = TimeSpan.FromMilliseconds(GetTickCount64());
			MessageBox.Show("System has been up for " + uptime.Days + " days and " + uptime.Hours + " hours.", "Uptime");
		}

		private void about(object sender, EventArgs e) // Clicked About
		{
			MessageBox.Show("This app fetches your current public IP address from <http://ipify.org> and your private IP addresses from your local interfaces. It also provides a latency check to <" + url + "> every " + interval + "s, and will alert you if your IP changes, latency becomes too bad, or your network connection drops. Log of connection issues is at <" + log_file + ">, configuration values can be found in the Registry at <HKCU\\Software\\NetTray>.\r\n\r\nProvided under the MIT License by Patrick Lambert <http://dendory.net>.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void latency(object sender, EventArgs e) // Clicked Latency
		{
			if(cur_latency != -1) // Latency is known, so show cached value
			{
				MessageBox.Show("Round trip to " + url + ": " + cur_latency.ToString() + "ms.", "Latency");
			}
			else // Latency is unknown, so try to ping
			{
				Ping p = new Ping();
				PingReply r = p.Send(url);
				if(r.Status == IPStatus.Success) // Got a reply
				{
					cur_latency = r.RoundtripTime;
					MessageBox.Show("Round trip to " + url + ": " + cur_latency.ToString() + "ms.", "Latency");
				}
				else // Ping timed out
				{
					MessageBox.Show("Unable to ping " + url + ".", "Latency");
				}
			}
		}

		private void interfaces(object sender, EventArgs e) // Clicked Interfaces
		{
			string details = ""; // Buffer for text
			try
			{
				foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) // Enumerate all interfaces from WMI
				{
					if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) // Only care about ethernet and wifi
					{
						details += ni.Name + "\r\n";
						foreach(UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses) // IP addresses
						{
							if(ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
							{
								details += "- IP: " + ip.Address.ToString() + "\r\n";
							}
							if(ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
							{
								details += "- IPv6: " + ip.Address.ToString() + "\r\n";
							}
						}
						foreach(GatewayIPAddressInformation gw in ni.GetIPProperties().GatewayAddresses) // Gateway
						{
							if(gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
							{
								details += "- Gateway: " + gw.Address.ToString() + "\r\n";
							}
						}
						details += "- Type: " + ni.NetworkInterfaceType.ToString() + " (" + ni.OperationalStatus.ToString() + ")\r\n";
						details += "- Speed: " + Int64.Parse(ni.Speed.ToString()) / 1000000 + " mbps\r\n";
						details += "\r\n";
					}
				}
			}
			catch (Exception ex)
			{
				details += "An error occured: " + ex.Message;
			}
			MessageBox.Show(details, "All interfaces");
		}

		protected override void OnLoad(EventArgs e) // Display the tray icon on load
		{
			Visible = false;
			ShowInTaskbar = false;
 			base.OnLoad(e);
		}

		protected override void Dispose(bool is_disposing) // Remove the tray icon on exit
		{
			if(is_disposing)
			{
				tray_icon.Dispose();
			}
 			base.Dispose(is_disposing);
		}

		private string get_private_ip() // Fetch private IP from WMI
		{
			string privip = "";
			try
			{
				foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
				{
					if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
					{
						foreach(UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
						{
							if(ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork || ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
							{
								privip = ip.Address.ToString();
							}
						}
					}
				}
			}
			catch
			{
				return "Unknown";
			}
			return privip;
		}

		private string get_public_ip() // Fetch public IP from ipify.org
		{
			try
			{
				System.Net.WebRequest req = System.Net.WebRequest.Create("https://api.ipify.org?format=text");
				System.Net.WebResponse resp = req.GetResponse();
				System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream());
				string response = sr.ReadToEnd().Trim();
				return response;
			}
			catch (Exception)
			{
				return "Unknown";
			}
		}
	}
}