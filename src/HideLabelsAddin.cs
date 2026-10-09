using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("EAHideConnectorLabels")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace EAHideConnectorLabels
{
    /// <summary>
    /// Enterprise Architect add-in: hides all labels of every connector
    /// the user draws on a diagram (EA_OnPostNewConnector).
    /// </summary>
    [ComVisible(true)]
    [Guid("6F3B2C1A-8E4D-4B7A-9C21-5D0E7A4F1B63")]
    [ProgId("EAHideConnectorLabels.Addin")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Addin
    {
        const string MenuRoot = "-&Hide Connector Labels";
        const string MenuToggle = "&Auto-hide labels on new connectors";
        const string MenuHideAll = "Hide labels of all connectors on &current diagram";
        const string SettingsKey = @"Software\EAHideConnectorLabels";

        // Connectors waiting to be processed once EA has finished creating them
        // (e.g. after the database Foreign Key dialog is closed).
        readonly List<int[]> pending = new List<int[]>();
        System.Windows.Forms.Timer timer;
        EA.Repository repo;
        DateTime pendingSince;

        // ---- Lifecycle -----------------------------------------------------

        public string EA_Connect(EA.Repository repository)
        {
            Log("EA_Connect");
            return "a string";
        }

        public void EA_Disconnect()
        {
            if (timer != null) timer.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // ---- Menu ----------------------------------------------------------

        public object EA_GetMenuItems(EA.Repository repository, string location, string menuName)
        {
            if (menuName == "") return MenuRoot;
            if (menuName == MenuRoot) return new string[] { MenuToggle, MenuHideAll };
            return "";
        }

        public void EA_GetMenuState(EA.Repository repository, string location, string menuName,
            string itemName, ref bool isEnabled, ref bool isChecked)
        {
            if (itemName == MenuToggle) { isEnabled = true; isChecked = Enabled; }
            else if (itemName == MenuHideAll) isEnabled = repository.GetCurrentDiagram() != null;
        }

        public void EA_MenuClick(EA.Repository repository, string location, string menuName, string itemName)
        {
            try
            {
                if (itemName == MenuToggle) Enabled = !Enabled;
                else if (itemName == MenuHideAll)
                {
                    EA.Diagram d = repository.GetCurrentDiagram();
                    if (d != null) Log("Hid labels on " + HideAllOnDiagram(repository, d.DiagramID) + " connectors");
                }
            }
            catch (Exception ex) { Log("MenuClick failed: " + ex); }
        }

        // ---- Event ---------------------------------------------------------

        public bool EA_OnPostNewConnector(EA.Repository repository, EA.EventProperties info)
        {
            try
            {
                // EA only passes ConnectorID; the connector was drawn on the current diagram.
                int connectorId = Convert.ToInt32(info.Get("ConnectorID").Value);
                EA.Diagram current = repository.GetCurrentDiagram();
                int diagramId = current != null ? current.DiagramID : 0;
                Log("OnPostNewConnector connector=" + connectorId + " diagram=" + diagramId + " enabled=" + Enabled);
                if (!Enabled || diagramId <= 0) return false;

                // Don't touch the diagram yet: EA may still show a dialog for the new
                // connector (e.g. Foreign Key) and would overwrite our change afterwards.
                repo = repository;
                pending.Add(new[] { connectorId, diagramId });
                pendingSince = DateTime.Now;
                if (timer == null)
                {
                    timer = new System.Windows.Forms.Timer();
                    timer.Interval = 300;
                    timer.Tick += OnTick;
                }
                timer.Start();
            }
            catch (Exception ex) { Log("OnPostNewConnector failed: " + ex); }
            return false;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (pending.Count == 0) { timer.Stop(); return; }

            // While a modal dialog is open, EA's main window is disabled: wait.
            IntPtr main = Process.GetCurrentProcess().MainWindowHandle;
            if (main != IntPtr.Zero && !IsWindowEnabled(main) && (DateTime.Now - pendingSince).TotalSeconds < 120)
                return;

            timer.Stop();
            int[][] work = pending.ToArray();
            pending.Clear();
            foreach (int[] p in work)
            {
                try
                {
                    HideLabels(repo, p[0], p[1], true);
                    Log("Hid labels of connector " + p[0]);
                }
                catch (Exception ex) { Log("Hide failed for connector " + p[0] + ": " + ex); }
            }
        }

        [DllImport("user32.dll")]
        static extern bool IsWindowEnabled(IntPtr hWnd);

        // ---- Core ----------------------------------------------------------

        /// <summary>Hides every label of one connector on one diagram.</summary>
        public static bool HideLabels(EA.Repository repository, int connectorId, int diagramId, bool reload)
        {
            // Persist the diagram first so the new link exists in the database
            // and nothing the user did on the diagram is lost by the reload below.
            if (reload) repository.SaveDiagram(diagramId);

            EA.Diagram diagram = repository.GetDiagramByID(diagramId);
            EA.DiagramLink link = FindLink(diagram, connectorId);
            if (link == null)
            {
                link = (EA.DiagramLink)diagram.DiagramLinks.AddNew("", "");
                link.ConnectorID = connectorId;
                link.Update();
            }
            ApplyHidden(link);
            diagram.DiagramLinks.Refresh();

            if (reload) repository.ReloadDiagram(diagramId);
            return true;
        }

        public static int HideAllOnDiagram(EA.Repository repository, int diagramId)
        {
            repository.SaveDiagram(diagramId);
            EA.Diagram diagram = repository.GetDiagramByID(diagramId);
            int n = 0;
            foreach (EA.DiagramLink link in diagram.DiagramLinks) { ApplyHidden(link); n++; }
            repository.ReloadDiagram(diagramId);
            return n;
        }

        static EA.DiagramLink FindLink(EA.Diagram diagram, int connectorId)
        {
            foreach (EA.DiagramLink l in diagram.DiagramLinks)
                if (l.ConnectorID == connectorId) return l;
            return null;
        }

        static void ApplyHidden(EA.DiagramLink link)
        {
            // Hide every label except the two multiplicities (LLB = source, LRB = target),
            // so 1 / 0..* / * stay visible at each end.
            link.HiddenLabels = false;
            link.Geometry = HideInGeometry(link.Geometry ?? "");
            link.Update();
        }

        // LLT/LRT = role names, LMT = name, LMB = stereotype, IRHS/ILHS = qualifiers.
        static readonly string[] LabelKeys = { "LLT", "LMT", "LMB", "LRT", "IRHS", "ILHS" };

        public static string HideInGeometry(string geometry)
        {
            foreach (string key in LabelKeys)
            {
                // Label sections look like "LMT=CX=40:CY=13:OX=0:OY=0:HDN=0:...;"
                geometry = Regex.Replace(geometry, @"(?<=(^|[;$])" + key + @"=)([^;]*)", m =>
                {
                    string v = m.Value;
                    if (v.Length == 0) return v;
                    if (Regex.IsMatch(v, @"(^|:)HDN=\d")) return Regex.Replace(v, @"(^|:)HDN=\d", "$1HDN=1");
                    return v + ":HDN=1";
                });
            }
            return geometry;
        }

        // ---- Settings & log ------------------------------------------------

        static bool Enabled
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SettingsKey))
                    return k == null || Convert.ToInt32(k.GetValue("Enabled", 1)) != 0;
            }
            set
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    k.SetValue("Enabled", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        static void Log(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(Addin).Assembly.Location);
                File.AppendAllText(Path.Combine(dir, "EAHideConnectorLabels.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
