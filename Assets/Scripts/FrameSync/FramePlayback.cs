using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TurnSyncModule
{
    public interface IKeyTurn
    {
        // Turn seq
        int seq
        {
            get;
            set;
        }

        // trigger time
        float time
        {
            get;
            set;
        }

        // indicate Turn type
        byte TurnT
        {
            get;
            set;
        }

        // indicate serialized data size
        int length
        {
            get;
            set;
        }

        void Sample ();
        void Serialize(BinaryWriter ser);
        void Deserialize(BinaryReader ser);
    }

    public abstract class KeyTurnBase : AbstractSmartObj, IKeyTurn
    {
        protected int _seq = 0;
        public int seq
        {
            get
            {
                return _seq;
            }
            set
            {
                _seq = value;
            }
        }

        protected float _time = 0f;
        public float time
        {
            get
            {
                return _time;
            }
            set
            {
                _time = value;
            }
        }

        protected byte _TurnT = 0;
        public byte TurnT
        {
            get
            {
                return _TurnT;
            }
            set
            {
                _TurnT = value;
            }
        }

        protected int _length = 0;
        virtual public int length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        // execute this key Turn
        abstract public void Sample();

        virtual public void Serialize (BinaryWriter ser)
        {
            ser.Write(_seq);
            ser.Write(_time);
            ser.Write(length);
        }

        virtual public void Deserialize (BinaryReader ser)
        {
            _seq = ser.ReadInt32();
            _time = ser.ReadSingle();
            length = ser.ReadInt32();
        }

        public override void OnRelease() { }        
    }

    public class TurnPlayback : Singleton<TurnPlayback>
    {
        public delegate IKeyTurn TurnCreator(byte TurnT, BinaryReader ser);

        protected enum EMode
        {
            Idle = 0,
            Record,
            PlayBack,
            PlayBack_FastFoward
        }

        protected EMode _mode = EMode.Idle;
        protected Queue<IKeyTurn> m_KeyTurnList = new Queue<IKeyTurn>();

        protected FileStream _fs = null;
        protected const int kRecordVersion = 1;

        public TurnCreator creator = null;
        private float _ticks = 0f;
        private uint autoSampleInterval = 0u;

        protected bool _auto = false;
        public bool Auto
        {
            get
            {
                return _auto;
            }
            set
            {
                _auto = value;
                if (_auto)
                    _ticks = Time.realtimeSinceStartup;
            }
        }

        public bool IsRecording
        {
            get
            {
                return _mode == EMode.Record;
            }
        }

        public bool IsPlayback
        {
            get
            {
                return _mode == EMode.PlayBack || _mode == EMode.PlayBack_FastFoward;
            }
        }

        public void BeginRecord()
        {
            if (_mode != EMode.Record)
            {
                _mode = EMode.Record;
                m_KeyTurnList.Clear();

                try
                {
                    string path = Application.dataPath + "/records/";
                    if (Directory.Exists(path) == false)
                    {
                        Directory.CreateDirectory(path);
                    }

                    path = path + "record_" + System.DateTime.Now.ToString("yyyy_mm_dd_hh_mm_ss") + ".rec";
                    _fs = File.Open(path, FileMode.OpenOrCreate);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _mode = EMode.Idle;
                }
            }
        }

        public void EndRecord ()
        {
            if (_mode == EMode.Record)
            {
                BinaryWriter ser = new BinaryWriter(_fs);
                ser.Write(kRecordVersion);
                ser.Write(TurnSyncService.instance.GetTurnSyncChr().TurnDelta);
                ser.Write(m_KeyTurnList.Count);
                while (m_KeyTurnList.Count > 0)
                {
                    IKeyTurn kf = m_KeyTurnList.Dequeue();
                    ser.Write(kf.TurnT);
                    kf.Serialize(ser);

                    if (kf is ISmartObj)
                        ((ISmartObj)kf).Release();
                }

                ser.Close();
                _fs.Close();
                _fs = null;

                _mode = EMode.Idle;
            }
        }

        public void BeginPlayback(string path, bool fastForward = false)
        {
            if (_mode != EMode.PlayBack)
            {
                if (_mode == EMode.Record)
                {
                    EndRecord();
                }

                try
                {
                    path = Application.dataPath + "/records/" + path + ".rec";
                    _fs = File.OpenRead(path);
                    LoadReplay(fastForward);
                    _mode = fastForward ? EMode.PlayBack_FastFoward : EMode.PlayBack;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // tick one Turn mannually.
        public void Sample()
        {
            if ( _mode == EMode.PlayBack || _mode == EMode.PlayBack_FastFoward)
            {
                if (m_KeyTurnList.Count > 0 )
                {
                    IKeyTurn kf = m_KeyTurnList.Dequeue();
                    kf.Sample();

                    if (kf is ISmartObj)
                        ((ISmartObj)kf).Release();
                }

                if (m_KeyTurnList.Count == 0 )
                {
                    _mode = EMode.Idle;
                }
            }
        }

        public void Update()
        {
            if (_mode == EMode.PlayBack || _mode == EMode.PlayBack_FastFoward)
            {
                if (_auto)
                {
                    if (_mode == EMode.PlayBack)
                    {
                        float t = Time.realtimeSinceStartup;
                        uint delta = (uint)((t - _ticks) * 1000f);

                        if (delta >= autoSampleInterval)
                        {
                            while (delta >= autoSampleInterval)
                            {
                                Sample();
                                delta -= autoSampleInterval;
                            }

                            _ticks = t - delta * 0.001f;
                        }
                    }
                    else if (_mode == EMode.PlayBack_FastFoward)
                    {
                        if (m_KeyTurnList.Count > 0)
                        {
                            int count = Math.Min(m_KeyTurnList.Count, 1);
                            for (int ii = 0; ii < count; ++ii)
                            {
                                Sample();
                            }
                        }
                    }
                }
            }
        }

        private void LoadReplay(bool fastForward = false)
        {
            BinaryReader ser = new BinaryReader(_fs);
            int ver = ser.ReadInt32();
            if ( ver > kRecordVersion )
            {
                throw new Exception("Unsupported record file version");
            }

            uint interval = ser.ReadUInt32();
            autoSampleInterval = fastForward ? 0u : interval;
            int count = ser.ReadInt32();
            for (int ii = 0; ii < count; ++ii)
            {
                byte TurnT = ser.ReadByte();
                IKeyTurn kf = creator != null ? creator(TurnT, ser) : null;
                if ( kf == null )
                {
                    throw new Exception("Unrecognized key Turn type, halt!");
                }
                else
                {
                    kf.Deserialize(ser);
                }

                m_KeyTurnList.Enqueue(kf);
            }

            ser.Close();
            _fs.Close();
        }

        public void AddKeyTurn(IKeyTurn keyTurn)
        {
            if (_mode == EMode.Record)
            {
                if (keyTurn != null)
                {
                    keyTurn.time = Time.realtimeSinceStartup;
                    keyTurn.seq = m_KeyTurnList.Count;
                    m_KeyTurnList.Enqueue(keyTurn);
                }
            }
        }

        public override void UnInit()
        {
            if (_mode == EMode.Record)
            {
                EndRecord();
            }
        }
    }
}