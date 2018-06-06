using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

namespace TurnSyncModule
{
    public partial class TurnSyncService : Singleton<TurnSyncService>
    {
        private TurnWindow m_turnWindow = null;
        private TurnSynchr m_turnSynchr = null;

        protected delegate void VoidCallback();

        static protected VoidCallback _internal_cleanup_callback;
        static protected bool _extension_preparion = false;

        private bool _willStopService = false;
        private int _targetStopTurnNo = 0;

        private bool m_isActive = false;
        public bool isActive
        {
            get
            {
                return m_isActive;
            }
        }
        //逻辑帧实际执行的时间
        public ulong LogicTurnTick
        {
            get
            {
                return m_turnSynchr.LogicTurnTick;
            }
        }

        public bool IsRepairing
        {
            get { return m_turnWindow.IsRepairing; }
        }
        
        public bool IsLaterTurnTracing
        {
            get { 
                if (m_turnWindow.IsLaterTurnTracing )
                {
                    m_turnWindow.IsLaterTurnTracing = m_turnSynchr.CurTurnNum < m_turnSynchr.EndTurnNum;                    
                }
                return m_turnWindow.IsLaterTurnTracing;
            }
        }

        public bool HasExecutableCommands
        {
            get { return m_turnSynchr.HasExecutableCommands; }
        }

        public uint BlockTurnWaitNum
        {
            get { return m_turnSynchr.BlockTurnWaitNum; }
        }

        public TurnSynchr GetTurnSyncChr()
        {
            return m_turnSynchr;
        }

        public TurnWindow GetTurnWindow()
        {
            return m_turnWindow;
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

        public TurnSyncService()
        {
            m_turnWindow = new TurnWindow();
            m_turnSynchr = new TurnSynchr();

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
                m_turnSynchr.StartSynchr(true);
                m_turnSynchr.bActive = _serviceMode == EServiceMode.E_SM_ONLINE || _serviceMode == EServiceMode.E_SM_PLAYBACK || _serviceMode == EServiceMode.E_SM_OB;              
            }
        }

        
        public void Reset()
        {
            m_turnWindow.Reset();
            m_turnSynchr.ResetSynchr();
        }

        public void StopService(int delayTurn = 0)
        {
            if (m_isActive && !_willStopService)
            {
                _willStopService = true;
                _targetStopTurnNo = (int)m_turnSynchr.CurTurnNum + delayTurn;
            }
        }

        public void Update()
        {
            if (m_isActive)
            {
                m_turnWindow.UpdateTurn();
                m_turnSynchr.UpdateTurn();
                if ( _willStopService )
                {
                    if (m_turnSynchr.CurTurnNum >= _targetStopTurnNo)
                    {
                        _willStopService = false;
                        m_isActive = false;
                    }
                }
            }
        }

        public override void UnInit() {
            m_turnWindow.Dispose();
            m_turnSynchr.Dispose();

            if (_internal_cleanup_callback != null)
                _internal_cleanup_callback();
        }

        public void PushTurnCommand (ITurnCommand cmd)
        {
            if ( cmd != null )
            { 
                m_turnSynchr.PushTurnCommand(cmd);
            }
        }
        
        public bool SetTurnIndex (uint TurnIndex)
        {
            return m_turnSynchr.SetKeyTurnIndex(TurnIndex);
        }
    }
}
