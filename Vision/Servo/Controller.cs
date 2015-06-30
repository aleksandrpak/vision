using System;
using System.IO.Ports;
using System.Threading;

namespace Servo
{
    public sealed class Controller : IDisposable
    {
        private const int AngleSpeedMsPerDegree = 50;

        private SerialPort _servo;

        public byte Angle { get; private set; }

        public bool IsConnected => _servo != null && _servo.IsOpen;

        public void Connect(string port)
        {
            _servo = new SerialPort(port, 9600);
            _servo.Open();

            _servo.Write(new byte[] { 90, 255 }, 0, 2);
            Thread.Sleep(200);

            Angle = 90;
        }

        public void Rotate(byte angle)
        {
            if (!IsConnected)
                return;

            var diff = Math.Abs(Angle - angle);

            _servo.Write(new byte[] { angle, 255 }, 0, 2);

            Thread.Sleep(diff * AngleSpeedMsPerDegree);

            Angle = angle;
        }

        ~Controller()
        {
            _servo?.Dispose();
        }

        public void Dispose()
        {
            _servo?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
