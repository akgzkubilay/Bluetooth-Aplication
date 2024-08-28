using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using NAudio.Wave;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace WindowsFormsApp2
{
    public partial class Form2 : Form
    {
        private BluetoothLEAdvertisementWatcher watcher = null;
        private List<DeviceInfo> devicesList = new List<DeviceInfo>();
        private string selectedDeviceId = null;
        private WaveInEvent waveIn;
        private WaveFileWriter waveFileWriter;
        private string outputFilePath = "recordedAudio.wav";

        public Form2()
        {
            InitializeComponent();
            button3.Click += Button3_Click;
            listView1.ItemSelectionChanged += ListView1_ItemSelectionChanged;
            buttonStartRecording.Click += StartRecordingButton_Click;
            buttonStopRecording.Click += StopRecordingButton_Click;
        }

        private void ListView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected)
            {
                selectedDeviceId = e.Item.SubItems[1].Text;
            }
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
        }

        private void BLE_StartScanner()
        {
            listView1.Items.Clear();
            devicesList.Clear();
            watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            watcher.Received += OnAdvertisementReceived;
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(1000);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(500);
            try
            {
                watcher.Start();
              //  MessageBox.Show("Watcher started successfully");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error starting watcher: {ex.Message}");
            }

        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            MessageBox.Show("başladık emmi ");
            string nameBuffer = args.Advertisement.LocalName;
            if (!string.IsNullOrEmpty(nameBuffer))
            {
                string deviceId = args.BluetoothAddress.ToString();

                var existingDevice = devicesList.FirstOrDefault(d => d.DeviceId == deviceId);
                if (existingDevice == null)
                {
                    int signalStrength = args.RawSignalStrengthInDBm;
                    devicesList.Add(new DeviceInfo(nameBuffer, deviceId, signalStrength));
                    UpdateListView();
                }
            }
        }

        private void UpdateListView()
        {
            listView1.Items.Clear();
            var sortedDevices = devicesList.OrderByDescending(d => d.SignalStrength);
            foreach (var device in sortedDevices)
            {
                string[] bilgiler = { device.Name, device.DeviceId, device.SignalStrength.ToString() + " dBm" };
                ListViewItem lst = new ListViewItem(bilgiler);
                listView1.Items.Add(lst);

                if (device.DeviceId == selectedDeviceId)
                {
                    lst.Selected = true;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            BLE_StartScanner();
        }

        private async void Button3_Click(object sender, EventArgs e)
        {
            // seçecek ürün olmadığı için buraya girmiyor !!!
            if (listView1.SelectedItems.Count > 0)
            {
                string device_id = listView1.SelectedItems[0].SubItems[1].Text;
                await ConnectToDevice(device_id);
            }
            else
            {
                MessageBox.Show("Select a device first.");
            }
        }

        private async Task ConnectToDevice(string device_id)
        {
            lblStatus.Text = "Connecting...";
            try
            {
                ulong bluetoothAddress = ulong.Parse(device_id);
                BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);

                if (device != null)
                {
                    var servicesResult = await device.GetGattServicesAsync();
                    if (servicesResult.Status == GattCommunicationStatus.Success)
                    {
                        var services = servicesResult.Services;
                        foreach (var service in services)
                        {
                            Console.WriteLine($"Service UUID: {service.Uuid}");

                            GattCharacteristicsResult characteristicsResult = await service.GetCharacteristicsAsync();
                            if (characteristicsResult.Status == GattCommunicationStatus.Success)
                            {
                                var characteristics = characteristicsResult.Characteristics;
                                foreach (var characteristic in characteristics)
                                {
                                    Console.WriteLine($"Characteristic UUID: {characteristic.Uuid}");

                                    if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                                    {
                                        GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                        if (status == GattCommunicationStatus.Success)
                                        {
                                            characteristic.ValueChanged += Charactristic_ValueChanged;
                                        }
                                    }
                                }
                            }
                        }
                        lblStatus.Text = "Connected and services retrieved";
                    }
                    else
                    {
                        lblStatus.Text = "Failed to get GATT services";
                    }
                }
                else
                {
                    lblStatus.Text = "Device not found";
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"An error occurred while connecting to the device: {ex.Message}");
            }
        }

        private void Charactristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // Handle the value changed event here if needed
        }

        private void StartRecordingButton_Click(object sender, EventArgs e)
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(44100, 1) // CD quality
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;

            waveFileWriter = new WaveFileWriter(outputFilePath, waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private void StopRecordingButton_Click(object sender, EventArgs e)
        {
            waveIn?.StopRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            waveFileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            waveFileWriter?.Flush();
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            waveFileWriter?.Dispose();
            waveFileWriter = null;
            waveIn?.Dispose();
            waveIn = null;
        }

        private void buttonStartRecording_Click(object sender, EventArgs e)
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(44100, 1) // CD quality
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;

            waveFileWriter = new WaveFileWriter(outputFilePath, waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private void buttonStopRecording_Click(object sender, EventArgs e)
        {
               waveIn?.StopRecording();
        }
    }

    public class DeviceInfo
    {
        public string Name { get; }
        public string DeviceId { get; }
        public int SignalStrength { get; set; }

        public DeviceInfo(string name, string deviceId, int signalStrength)
        {
            Name = name;
            DeviceId = deviceId;
            SignalStrength = signalStrength;
        }
    }
}
