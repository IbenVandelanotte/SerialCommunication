using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace SerialCommunication
{
    public partial class Form1 : Form
    {
        private SerialPort serialPortArduino = new SerialPort() { ReadTimeout = 1000, WriteTimeout = 1000 };
        private System.Windows.Forms.Timer timerOefening3;
        private System.Windows.Forms.Timer timerOefening4;
        private System.Windows.Forms.Timer timerOefening5;
        private bool suppressDigital2Events = false;

        public Form1()
        {
            InitializeComponent();
            this.trackBarPWM9.Scroll += new System.EventHandler(this.trackBarPWM9_Scroll);

            // timer for Oefening3: 1000 ms, initially disabled
            timerOefening3 = new System.Windows.Forms.Timer() { Interval = 1000, Enabled = false };
            timerOefening3.Tick += new System.EventHandler(this.timerOefening3_Tick);

            // timer for Oefening4: 1000 ms, initially disabled
            timerOefening4 = new System.Windows.Forms.Timer() { Interval = 1000, Enabled = false };
            timerOefening4.Tick += new System.EventHandler(this.timerOefening4_Tick);

            // timer for Oefening5: 1000 ms, initially disabled
            timerOefening5 = new System.Windows.Forms.Timer() { Interval = 1000, Enabled = false };
            timerOefening5.Tick += new System.EventHandler(this.timerOefening5_Tick);

            // handle tab selection changes to enable/disable the timers
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // Try to set pictureBox1 image from resources; if unavailable, hide the picture box to avoid exceptions.
                try
                {
                    var img = SerialCommunication.Properties.Resources.digital_out;
                    if (img != null)
                    {
                        pictureBox1.Image = img;
                        pictureBox1.Visible = true;
                    }
                    else
                    {
                        pictureBox1.Visible = false;
                    }
                }
                catch (Exception)
                {
                    pictureBox1.Visible = false;
                    try { labelStatus.Text = "Image resource niet beschikbaar"; } catch { }
                }

                string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();
                comboBoxPoort.Items.Clear();
                comboBoxPoort.Items.AddRange(portNames);
                if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;

                comboBoxBaudrate.SelectedIndex = comboBoxBaudrate.Items.IndexOf("115200");
            }
            catch (Exception)
            { }
            // Disable digital checkboxes until connected
            checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
        }

        private void cboPoort_DropDown(object sender, EventArgs e)
        {
            try
            {
                string selected = (string)comboBoxPoort.SelectedItem;
                string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();

                comboBoxPoort.Items.Clear();
                comboBoxPoort.Items.AddRange(portNames);

                comboBoxPoort.SelectedIndex = comboBoxPoort.Items.IndexOf(selected);
            }
            catch (Exception)
            {
                if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;
            }
        }

        private async void buttonConnect_Click(object sender, EventArgs e)
        {
            if (!serialPortArduino.IsOpen)
            {
                try
                {
                    string portName = comboBoxPoort.SelectedItem?.ToString() ?? comboBoxPoort.Text;
                    if (string.IsNullOrWhiteSpace(portName))
                    {
                        labelStatus.Text = "Selecteer een poort.";
                        return;
                    }
                    serialPortArduino.PortName = portName;

                    int baud;
                    if (!int.TryParse(comboBoxBaudrate.SelectedItem?.ToString() ?? comboBoxBaudrate.Text, out baud))
                        baud = 115200;
                    serialPortArduino.BaudRate = baud;

                    serialPortArduino.DataBits = (int)numericUpDownDatabits.Value;

                    if (radioButtonParityNone.Checked) serialPortArduino.Parity = Parity.None;
                    else if (radioButtonParityEven.Checked) serialPortArduino.Parity = Parity.Even;
                    else if (radioButtonParityOdd.Checked) serialPortArduino.Parity = Parity.Odd;
                    else if (radioButtonParityMark.Checked) serialPortArduino.Parity = Parity.Mark;
                    else if (radioButtonParitySpace.Checked) serialPortArduino.Parity = Parity.Space;

                    if (radioButtonStopbitsOne.Checked) serialPortArduino.StopBits = StopBits.One;
                    else if (radioButtonStopbitsOnePointFive.Checked) serialPortArduino.StopBits = StopBits.OnePointFive;
                    else if (radioButtonStopbitsTwo.Checked) serialPortArduino.StopBits = StopBits.Two;
                    else serialPortArduino.StopBits = StopBits.None;

                    if (radioButtonHandshakeNone.Checked) serialPortArduino.Handshake = Handshake.None;
                    else if (radioButtonHandshakeRTS.Checked) serialPortArduino.Handshake = Handshake.RequestToSend;
                    else if (radioButtonHandshakeRTSXonXoff.Checked) serialPortArduino.Handshake = Handshake.RequestToSendXOnXOff;
                    else if (radioButtonHandshakeXonXoff.Checked) serialPortArduino.Handshake = Handshake.XOnXOff;

                    serialPortArduino.RtsEnable = checkBoxRtsEnable.Checked;
                    serialPortArduino.DtrEnable = checkBoxDtrEnable.Checked;

                    serialPortArduino.Open();

                    // send ping and wait for pong (use Task.Run to avoid blocking UI thread)
                    serialPortArduino.WriteLine("ping");
                    string response = await Task.Run(() => serialPortArduino.ReadLine());
                    if (response != null && response.Trim().Equals("pong", StringComparison.OrdinalIgnoreCase))
                    {
                        radioButtonVerbonden.Checked = true;
                        buttonConnect.Text = "Disconnect";
                        labelStatus.Text = $"Verbonden: {serialPortArduino.PortName}";
                        checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = true;
                    }
                    else
                    {
                        labelStatus.Text = $"Onverwacht antwoord: {response}";
                        if (serialPortArduino.IsOpen) serialPortArduino.Close();
                    }
                }
                catch (TimeoutException)
                {
                    labelStatus.Text = "Timeout: geen antwoord op ping.";
                    if (serialPortArduino.IsOpen) serialPortArduino.Close();
                    checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
                }
                catch (UnauthorizedAccessException uaex)
                {
                    labelStatus.Text = "Geen toegang tot poort: " + uaex.Message + " — sluit andere applicaties die de poort gebruiken of kies een andere poort.";
                    if (serialPortArduino.IsOpen) serialPortArduino.Close();
                    checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
                }
                catch (IOException ioex)
                {
                    labelStatus.Text = "IO-fout bij communicatie: " + ioex.Message + " — controleer kabel/poort en probeer opnieuw.";
                    if (serialPortArduino.IsOpen) serialPortArduino.Close();
                    checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
                }
                catch (Exception ex)
                {
                    labelStatus.Text = "Fout bij verbinden: " + ex.Message;
                    if (serialPortArduino.IsOpen) serialPortArduino.Close();
                    checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
                }
            }
            else
            {
                try
                {
                    serialPortArduino.Close();
                }
                catch { }

                radioButtonVerbonden.Checked = false;
                buttonConnect.Text = "Connect";
                labelStatus.Text = "Niet verbonden";
                checkBoxDigital2.Enabled = checkBoxDigital3.Enabled = checkBoxDigital4.Enabled = false;
            }
        }

        // CheckedChanged handlers for Digital checkboxes
        private void checkBoxDigital2_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (suppressDigital2Events) return;
                if (!serialPortArduino.IsOpen)
                {
                    labelStatus.Text = "Niet verbonden";
                    return;
                }
                string cmd = checkBoxDigital2.Checked ? "set d2 high" : "set d2 low";
                serialPortArduino.WriteLine(cmd);
                labelStatus.Text = "Verstuurd: " + cmd;
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Fout bij verzenden: " + ex.Message;
            }
        }

        private void checkBoxDigital3_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen)
                {
                    labelStatus.Text = "Niet verbonden";
                    return;
                }
                string cmd = checkBoxDigital3.Checked ? "set d3 high" : "set d3 low";
                serialPortArduino.WriteLine(cmd);
                labelStatus.Text = "Verstuurd: " + cmd;
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Fout bij verzenden: " + ex.Message;
            }
        }

        private void checkBoxDigital4_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen)
                {
                    labelStatus.Text = "Niet verbonden";
                    return;
                }
                string cmd = checkBoxDigital4.Checked ? "set d4 high" : "set d4 low";
                serialPortArduino.WriteLine(cmd);
                labelStatus.Text = "Verstuurd: " + cmd;
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Fout bij verzenden: " + ex.Message;
            }
        }

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (tabControl.SelectedTab == tabPageOefening3)
                    timerOefening3.Enabled = true;
                else
                    timerOefening3.Enabled = false;

                if (tabControl.SelectedTab == tabPageOefening4)
                    timerOefening4.Enabled = true;
                else
                    timerOefening4.Enabled = false;

                if (tabControl.SelectedTab == tabPageOefening5)
                    timerOefening5.Enabled = true;
                else
                    timerOefening5.Enabled = false;
            }
            catch (Exception ex)
            {
                try { labelStatus.Text = "Fout in tabcontrol handler: " + ex.Message; } catch { }
            }
        }

        private void timerOefening3_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen) return;

                // remove previous Arduino answers
                try { serialPortArduino.ReadExisting(); } catch { }

                // digital5
                try
                {
                    serialPortArduino.WriteLine("get d5");
                    string response = serialPortArduino.ReadLine();
                    response = (response ?? string.Empty).Trim();
                    if (response.Contains(":")) response = response.Split(':').Last().Trim();
                    else if (response.Contains(" ")) response = response.Split(' ').Last().Trim();
                    radioButtonDigital5.Checked = (response == "1");
                }
                catch { /* ignore individual read errors */ }

                // digital6
                try
                {
                    serialPortArduino.WriteLine("get d6");
                    string response = serialPortArduino.ReadLine();
                    response = (response ?? string.Empty).Trim();
                    if (response.Contains(":")) response = response.Split(':').Last().Trim();
                    else if (response.Contains(" ")) response = response.Split(' ').Last().Trim();
                    radioButtonDigital6.Checked = (response == "1");
                }
                catch { }

                // digital7
                try
                {
                    serialPortArduino.WriteLine("get d7");
                    string response = serialPortArduino.ReadLine();
                    response = (response ?? string.Empty).Trim();
                    if (response.Contains(":")) response = response.Split(':').Last().Trim();
                    else if (response.Contains(" ")) response = response.Split(' ').Last().Trim();
                    radioButtonDigital7.Checked = (response == "1");
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { labelStatus.Text = "Fout timerOefening3: " + ex.Message; } catch { }
            }
        }

        private void timerOefening4_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen) return;

                // read and parse any incoming lines (e.g., firmware periodic status)
                try
                {
                    string existing = serialPortArduino.ReadExisting();
                    if (!string.IsNullOrEmpty(existing))
                    {
                        var lines = existing.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string l = line.Trim();
                            if (l.StartsWith("desired:"))
                            {
                                string v = l.Substring("desired:".Length).Trim();
                                try { labelGewensteTemp.Text = v; } catch { }
                            }
                            else if (l.StartsWith("current:"))
                            {
                                string v = l.Substring("current:".Length).Trim();
                                try { labelHuidigeTemp.Text = v; } catch { }
                            }
                        }
                    }
                }
                catch { }

                // also request raw analog0 value (existing behavior)
                try
                {
                    serialPortArduino.WriteLine("get a0");
                    string response = serialPortArduino.ReadLine();
                    response = (response ?? string.Empty).Trim();
                    if (response.Contains(":")) response = response.Split(':').Last().Trim();
                    else if (response.Contains(" ")) response = response.Split(' ').Last().Trim();
                    labelAnalog0.Text = response;
                }
                catch { /* ignore individual read errors */ }

                // query LED (d2) status and update checkbox without triggering change handler
                try
                {
                    serialPortArduino.WriteLine("get d2");
                    string response = serialPortArduino.ReadLine();
                    response = (response ?? string.Empty).Trim();
                    if (response.Contains(":")) response = response.Split(':').Last().Trim();
                    else if (response.Contains(" ")) response = response.Split(' ').Last().Trim();
                    suppressDigital2Events = true;
                    checkBoxDigital2.Checked = (response == "1");
                    suppressDigital2Events = false;
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { labelStatus.Text = "Fout timerOefening4: " + ex.Message; } catch { }
            }
        }

        private void timerOefening5_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen) { labelStatus.Text = "Niet verbonden"; return; }

                // clear any previous buffered data
                try { serialPortArduino.ReadExisting(); } catch { }

                int raw0 = -1;
                int raw1 = -1;

                try
                {
                    serialPortArduino.WriteLine("get a0");
                    string response0 = serialPortArduino.ReadLine();
                    response0 = (response0 ?? string.Empty).Trim();
                    if (response0.Contains(":")) response0 = response0.Split(':').Last().Trim();
                    else if (response0.Contains(" ")) response0 = response0.Split(' ').Last().Trim();
                    int.TryParse(response0, out raw0);
                }
                catch (TimeoutException) { /* ignore individual timeout */ }

                try
                {
                    serialPortArduino.WriteLine("get a1");
                    string response1 = serialPortArduino.ReadLine();
                    response1 = (response1 ?? string.Empty).Trim();
                    if (response1.Contains(":")) response1 = response1.Split(':').Last().Trim();
                    else if (response1.Contains(" ")) response1 = response1.Split(' ').Last().Trim();
                    int.TryParse(response1, out raw1);
                }
                catch (TimeoutException) { /* ignore */ }

                double slopeDesired = 40.0 / 1023.0; // 5..45°C
                double offsetDesired = 5.0;
                double slopeCurrent = 500.0 / 1023.0; // 0..500°C

                bool haveDesired = raw0 >= 0;
                bool haveCurrent = raw1 >= 0;

                double desired = 0.0;
                double current = 0.0;

                if (haveDesired)
                {
                    desired = raw0 * slopeDesired + offsetDesired;
                    try { labelGewensteTemp.Text = desired.ToString("F1") + " °C"; } catch { }
                }

                if (haveCurrent)
                {
                    current = raw1 * slopeCurrent;
                    try { labelHuidigeTemp.Text = current.ToString("F1") + " °C"; } catch { }
                }

                if (haveDesired && haveCurrent)
                {
                    bool shouldBeOn = current < desired;
                    try
                    {
                        // only send command if state differs to reduce serial traffic
                        if (checkBoxDigital2.Checked != shouldBeOn)
                        {
                            string cmd = shouldBeOn ? "set d2 high" : "set d2 low";
                            serialPortArduino.WriteLine(cmd);
                        }
                        suppressDigital2Events = true;
                        checkBoxDigital2.Checked = shouldBeOn;
                        suppressDigital2Events = false;
                    }
                    catch { }
                }
            }
            catch (TimeoutException)
            {
                try { labelStatus.Text = "Timeout tijdens uitlezen."; } catch { }
            }
            catch (System.IO.IOException ioex)
            {
                try { labelStatus.Text = "IO-fout: " + ioex.Message; } catch { }
            }
            catch (Exception ex)
            {
                try { labelStatus.Text = "Fout timerOefening5: " + ex.Message; } catch { }
            }
        }

        private void trackBarPWM9_Scroll(object sender, EventArgs e)
        {
            try
            {
                if (!serialPortArduino.IsOpen)
                {
                    labelStatus.Text = "Niet verbonden";
                    return;
                }

                string cmd = "set pwm9 " + trackBarPWM9.Value.ToString();
                serialPortArduino.WriteLine(cmd);
                labelStatus.Text = "Verstuurd: " + cmd;
            }
            catch (Exception ex)
            {
                labelStatus.Text = "Fout bij verzenden: " + ex.Message;
            }
        }  
    }
}
