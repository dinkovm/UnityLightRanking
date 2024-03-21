using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Identifier : MonoBehaviour
{
    public uint value = 0;

    struct Tracker<T>
    {
        T value;
        bool dirty;

        public void Set(T in_value)
        {
            if ((value == null) || !value.Equals(in_value))
            {
                value = in_value;
                dirty = true;
            }
        }

        public bool Get(out T out_value)
        {
            bool out_dirty = dirty;
            dirty = false;
            out_value = value;
            return out_dirty;
        }
    }

    static private uint next = 1;
    private Tracker<Vector3> pos;
    private Tracker<Quaternion> rot;

    public void Start()
    {
        pos.Set(transform.position);
        rot.Set(transform.rotation);
    }

    public void Reset()
    {
        value = next++;
    }

    public void Update()
    {
        pos.Set(transform.position);
        rot.Set(transform.rotation);
    }

    public bool GetPos(out Vector3 out_pos)
    {
        return pos.Get(out out_pos);
    }

    public bool GetRot(out Quaternion out_pos)
    {
        return rot.Get(out out_pos);
    }
}
