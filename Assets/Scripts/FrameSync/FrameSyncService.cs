using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

namespace FrameSyncModule
{
    public partial class FrameSyncService : Singleton<FrameSyncService>
    {
        private FrameWindow m_frameWindow = null;
        private FrameSynchr m_frameSynchr = null;

        protected delegate void VoidCallback();

        static protected VoidCallback _internal_cleanup_callback;
        static protected bool _extension_preparion = false;

        private bool _willStopService = false;
        private int _targetStopFrameNo = 0;

        private bool m_isActive = false;
        public bool isActive
        {
            get
            {
                return m_isActive;
            }
        }
        //逻辑帧实际执行的时间
        public ulong LogicFrameTick
        {
            get
            {
                return m_frameSynchr.LogicFrameTick;
            }
        }

        public bool IsRepairing
        {
            get { return m_frameWindow.IsRepairing; }
        }
        
        public bool IsLaterFrameTracing
        {
            get { 
                if (m_frameWindow.IsLaterFrameTracing )
                {
                    m_frameWindow.IsLaterFrameTracing = m_frameSynchr.CurFrameNum < m_frameSynchr.EndFrameNum;                    
                }
                return m_frameWindow.IsLaterFrameTracing;
            }
        }

        public bool HasExecutableCommands
        {
            get { return m_frameSynchr.HasExecutableCommands; }
        }

        public uint BlockFrameWaitNum
        {
            get { return m_frameSynchr.BlockFrameWaitNum; }
        }

        public FrameSynchr GetFrameSyncChr()
        {
            return m_frameSynchr;
        }

        public FrameWindow GetFrameWindow()
        {
            return m_frameWindow;
        }

        public enum EServiceMode
        {
            E_SM_LOCALLY = 0,
            E_SM_ONLINE,
            E_SM_PLAYBACK, // video recording
            E_SM_OB
        }

        private EServiceMode _serviceMode = EServiceMode.E_SM_LOCALLY;
        public EServiceMode ServiceMode
        {
            get
            {
                return _serviceMode;
            }
            set
            {
                _serviceMode = value;
            }
        }

        public FrameSyncService()
        {
            m_frameWindow = new FrameWindow();
            m_frameSynchr = new FrameSynchr();

            if (!_extension_preparion)
            {
                _extension_preparion = true;
                Type classT = GetType();
                MethodInfo[] methods = classT.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                Type cleanupFuncT = typeof(GenericCleanupAttribute);
                for (int ii = 0; ii < methods.Length; ++ii)
                {
                    object[] attrs = methods[ii].GetCustomAttributes(cleanupFuncT, true);
                    for (int jj = 0; jj < attrs.Length; jj++)
                    {
                        if (attrs[jj].GetType() == cleanupFuncT)
                        {
                            _internal_cleanup_callback += (VoidCallback) Delegate.CreateDelegate(typeof(VoidCallback), this, methods[ii].Name);
                            break;
                        }
                    }
                }
            }
        }
        
        public void StartService()
        {
            if (!m_isActive)
            {
                m_isActive = true;              
                m_frameSynchr.StartSynchr(true);
                m_frameSynchr.bActive = _serviceMode == EServiceMode.E_SM_ONLINE || _serviceMode == EServiceMode.E_SM_PLAYBACK || _serviceMode == EServiceMode.E_SM_OB;              
            }
        }

        
        public void Reset()
        {
            m_frameWindow.Reset();
            m_frameSynchr.ResetSynchr();
        }

        public void StopService(int delayFrame = 0)
        {
            if (m_isActive && !_willStopService)
            {
                _willStopService = true;
                _targetStopFrameNo = (int)m_frameSynchr.CurFrameNum + delayFrame;
            }
        }

        public void Update()
        {
            if (m_isActive)
            {
                m_frameWindow.UpdateFrame();
                m_frameSynchr.UpdateFrame();
                if ( _willStopService )
                {
                    if (m_frameSynchr.CurFrameNum >= _targetStopFrameNo)
                    {
                        _willStopService = false;
                        m_isActive = false;
                    }
                }
            }
        }

        public override void UnInit() {
            m_frameWindow.Dispose();
            m_frameSynchr.Dispose();

            if (_internal_cleanup_callback != null)
                _internal_cleanup_callback();
        }

        //保存本地和服务器帧命令
        public void PushFrameCommand (IFrameCommand cmd)
        {
            if ( cmd != null )
            { 
                m_frameSynchr.PushFrameCommand(cmd);
            }
        }
        
        public bool SetFrameIndex (uint frameIndex)
        {
            return m_frameSynchr.SetKeyFrameIndex(frameIndex);
        }
    }
}
