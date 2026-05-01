using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
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

        public Form1()
        {
            InitializeComponent();
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
                }
                catch (Exception ex)
                {
                    labelStatus.Text = "Fout bij verbinden: " + ex.Message;
                    if (serialPortArduino.IsOpen) serialPortArduino.Close();
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
            }
        }

        // CheckedChanged handlers for Digital checkboxes
        private void checkBoxDigital2_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
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
    }
}
