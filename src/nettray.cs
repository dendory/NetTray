//
// NetTray - (C) 2016 Patrick Lambert - http://dendory.net - Provided under the MIT License
//

using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

[assembly: AssemblyTitle("NetTray")]
[assembly: AssemblyCopyright("(C) 2016 Patrick Lambert")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace NetTrayNS
{
	public class NetTray : Form
	{
		private NotifyIcon tray_icon;
		private ContextMenu tray_menu;
		private int interval = 900; // interval between polling, in seconds
		static System.Timers.Timer tray_timer;

		[STAThread]
		static void Main(string[] args)
		{
			Application.Run(new NetTray());
		}

		public NetTray()
		{
			tray_menu = new ContextMenu();
			tray_menu.MenuItems.Add("Interfaces", interfaces);
			tray_menu.MenuItems.Add("Latency", latency);
			tray_menu.MenuItems.Add("Refresh", refresh);
			tray_menu.MenuItems.Add("About", about);
			tray_menu.MenuItems.Add("-");
			tray_menu.MenuItems.Add("Exit", exit);
			tray_icon = new NotifyIcon();
			tray_icon.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("res.notify_icon"));
			tray_icon.ContextMenu = tray_menu;
			tray_icon.Visible = true;
			tray_timer = new System.Timers.Timer(interval * 1000);
			tray_timer.Elapsed += new System.Timers.ElapsedEventHandler(loop);
			tray_icon.Text = "Fetching...";
			tray_icon.Text = "Private IP: " + get_private_ip() + "\nPublic IP: " + get_public_ip();
			tray_timer.Start();
		}

		private void exit(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void refresh(object sender, EventArgs e)
		{
			tray_icon.Text = "Fetching...";
			tray_icon.Text = "Private IP: " + get_private_ip() + "\nPublic IP: " + get_public_ip();
		}

		private void about(object sender, EventArgs e)
		{
			MessageBox.Show("This app fetches your current public IP address from <http://ipify.org> and your private IP addresses from your local interfaces. It also provides a Ping function to <http://google.com>. Provided under the MIT License by Patrick Lambert <http://dendory.net>.", "NetTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void latency(object sender, EventArgs e)
		{
			Ping p = new Ping();
			PingReply r = p.Send("www.google.com");
			if(r.Status == IPStatus.Success)
			{
				MessageBox.Show("Round trip to Google: " + r.RoundtripTime.ToString() + "ms.", "Latency");
			}
			else
			{
				MessageBox.Show("Unable to ping Google.", "Latency");
			}
		}

		private void interfaces(object sender, EventArgs e)
		{
			string details = "";
			try
			{
				foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
				{
					if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
					{
						details += ni.Name + "\n";
						foreach(UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
						{
							if(ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
							{
								details += "- IP: " + ip.Address.ToString() + "\n";
							}
							if(ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
							{
								details += "- IPv6: " + ip.Address.ToString() + "\n";
							}
						}
						foreach(GatewayIPAddressInformation gw in ni.GetIPProperties().GatewayAddresses)
						{
							if(gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
							{
								details += "- Gateway: " + gw.Address.ToString() + "\n";
							}
						}
						details += "- Type: " + ni.NetworkInterfaceType.ToString() + " (" + ni.OperationalStatus.ToString()  + ")\n";
						details += "- Speed: " + Int64.Parse(ni.Speed.ToString()) / 1000000 + " mbps\n";
						details += "\n";
					}
				}
			}
			catch (Exception ex)
			{
				details += "An error occured: " + ex.Message;
			}
			MessageBox.Show(details, "All interfaces");
		}

		protected override void OnLoad(EventArgs e)
		{
			Visible = false;
			ShowInTaskbar = false;
 			base.OnLoad(e);
		}

		protected override void Dispose(bool is_disposing)
		{
			if(is_disposing)
			{
				tray_icon.Dispose();
			}
 			base.Dispose(is_disposing);
		}

		private string get_private_ip()
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

		private string get_public_ip()
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

		private void loop(object sender, System.Timers.ElapsedEventArgs e)
		{
			tray_timer.Stop();
			tray_icon.Text = "Private IP: " + get_private_ip() + "\nPublic IP: " + get_public_ip();
			tray_timer.Start();
		}
	}
}