﻿using System;
using System.Numerics;

namespace Ryujinx.Motion
{
    public class MotionInput
    {
        private readonly MotionSensorFilter _filter;

        private int _calibrationFrame = 0;

        public ulong   TimeStamp     { get; set; }
        public Vector3 Accelerometer { get; set; }
        public Vector3 Gyroscrope    { get; set; }
        public Vector3 Rotation      { get; set; }

        public MotionInput()
        {
            Accelerometer = new Vector3();
            Gyroscrope    = new Vector3();
            Rotation      = new Vector3();

            // TODO : RE the correct filter
            _filter = new MotionSensorFilter(1 / 60f);
        }

        public void Update(Vector3 accel, Vector3 gyro, ulong timestamp, int sensitivity, float deadzone)
        {
            if (gyro.Length() <= deadzone && accel.Length() >= 0.9 && accel.Z <= -0.9)
            {
                _calibrationFrame++;

                if (_calibrationFrame >= 90)
                {
                    gyro = new Vector3();

                    Rotation = new Vector3();

                    _filter.Quaternion.W = 0;

                    _calibrationFrame = 0;
                }
            }
            else
            {
                _calibrationFrame = 0;
            }

            Accelerometer = accel;

            if (gyro.Length() < deadzone)
            {
                gyro = new Vector3(0, 0, 0);
            }

            gyro *= sensitivity / 100f;

            Gyroscrope = gyro;

            float deltaTime = (timestamp - TimeStamp) / 1000000f;

            Vector3 deltaGyro = gyro * deltaTime;

            if (TimeStamp != 0)
            {
                Rotation += deltaGyro;
            }

            gyro.X = DegreeToRad(gyro.X);
            gyro.Y = -DegreeToRad(gyro.Y);
            gyro.Z = DegreeToRad(gyro.Z);

            //accel *= -1;
            accel.Y *= -1;

            _filter.SamplePeriod = TimeStamp == 0 ? 1 / 60f : deltaTime;
            //_filter.Update(gyro.X, -gyro.Y, -gyro.Z, accel.X, -accel.Y, -accel.Z);
            _filter.Update(accel, gyro);

            TimeStamp = timestamp;
        }

        public Matrix4x4 GetOrientation()
        {
            var filteredQuat = _filter.Quaternion;

            Quaternion quaternion = new Quaternion(filteredQuat.Y, filteredQuat.X, filteredQuat.W, filteredQuat.Z);

            return Matrix4x4.CreateFromQuaternion(quaternion);
        }

        private float DegreeToRad(float degree)
        {
            return degree / 180 * MathF.PI;
        }
    }
}
