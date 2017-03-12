using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;

namespace CustomCameraVScript
{
    class Transform
    {
        public Transform()
        {

        }

        public Transform(Vector3 position, Vector3 eulerAngles)
        {
            this.position = position;
            this.rotation = eulerAngles;
            this.quaternion = Quaternion.Euler(eulerAngles);
        }

        public Transform(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.quaternion = rotation;
            this.rotation = rotation.ToEulerAngles();
        }

        public Vector3 position;
        public Vector3 rotation;
        public Quaternion quaternion;

        public void PointAt(Vector3 target)
        {
            var res = Mathr.LookAt(position, target);
            quaternion = res;
            rotation = res.ToEulerAngles();
        }
    }
}
