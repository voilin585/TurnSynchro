using System;
using System.Collections.Generic;
using UnityEngine;

public interface ISmartObj
{
    int index
    {
        get;
        set;
    }

    ObjectCachePool.PoolHandle handle
    {
        get;
        set;
    }

    void Reset();
    void OnRelease();
}

public abstract class AbstractSmartObj : ISmartObj
{
    private int _index = -1;
    public int index
    {
        get
        {
            return _index;
        }
        set
        {
            _index = value;
        }
    }

    private ObjectCachePool.PoolHandle _handle = null;
    public ObjectCachePool.PoolHandle handle
    {
        get
        {
            return _handle;
        }
        set
        {
            _handle = value;
        }
    }

    virtual public void Reset() { }
    abstract public void OnRelease();
}


public static class PoolHandleExtension
{
    public static void Release(this ISmartObj obj)
    {
        if (obj != null)
        {
            if ( obj.handle == null )
            {
                Debug.LogError("PoolHandle: target object is not initialized from ObjectCachePool type:"+obj.GetType());
                return;
            }

            obj.handle.Release(obj.index);
            obj.OnRelease();
        }
    }
}

public class ObjectCachePool : Singleton<ObjectCachePool> 
{       
    public class PoolHandle
    {
        private int _fetchIndex = 0;
        private ISmartObj[] _dataObjList = null;
        private byte[] _stateList = null;
        private int _length = 0;
		private int _allocGran = 0;
        private Type _classT = null;

        public bool IsValid
        {
            get
            {
                return _length > 0;
            }
        }

		public PoolHandle (Type classT, int size, int allocGran)
        {
            _length = size;
            _allocGran = allocGran;
            _classT = classT;

            if (_length > 0)
            {
                _dataObjList = new ISmartObj[_length];
                _stateList = new byte[_length];
                for ( int ii = 0; ii < _length; ++ii)
                {
                    ISmartObj obj = (ISmartObj)Activator.CreateInstance(classT);
                    if ( obj == null )
                    {
                        _dataObjList = null;
                        _length = 0;
                        _stateList = null;
                        return;
                    }

                    _stateList[ii] = 0;
                    _dataObjList[ii] = obj;
                    obj.index = ii;
                    obj.handle = this;
                }
            }
        }

        public ISmartObj Fetch()
        {
            if (_length > 0)
            {
                int count = 0;                
                while (count < _length && _stateList[_fetchIndex] != 0)
                {
                    _fetchIndex = (_fetchIndex + 1) % _length;
                    ++count;
                }              

                if ( _stateList[_fetchIndex] == 0 )
                {
                    int id = _fetchIndex;
                    _stateList[_fetchIndex] = 1;
                    _fetchIndex = (_fetchIndex + 1) % _length;
                    _dataObjList[id].Reset();
                    return _dataObjList[id];
                }
                else
                {
                    // enlarge the buffer
                    Grow();   
                
                    if ( _stateList[_fetchIndex] == 0 )
                    {
                        int id = _fetchIndex;
                        _stateList[_fetchIndex] = 1;
                        _fetchIndex = (_fetchIndex + 1) % _length;
                        _dataObjList[id].Reset();
                        return _dataObjList[id];
                    }
                }
            }

            return null;
        }

        private void Grow()
        {
            int len = _length;
			_length += _allocGran;
            ISmartObj[] t = new ISmartObj[_length];
            byte[] s = new byte[_length];

            Array.Copy(_dataObjList, t, len);
            Array.Copy(_stateList, s, len);

            _fetchIndex = len;
            for ( int ii = len; ii < _length; ++ii )
            {               
                ISmartObj obj = (ISmartObj)Activator.CreateInstance(_classT);
                t[ii] = obj;
                s[ii] = 0;
                obj.index = ii;
                obj.handle = this;
            }

            _stateList = s;
            _dataObjList = t;

#if UNITY_EDITOR
            Debug.LogWarning(string.Format("ObjectCachePool: {0}.PoolSize has been enlarged from {1} to {2}", _classT.ToString(), len, _length));
#endif
        }

        public void Release(int index)
        {
            if ( index >= 0 && index < _length)
                _stateList[index] = 0;
        }
    }

    private Dictionary<Type, PoolHandle> _objectPool = new Dictionary<Type, PoolHandle>();
	
	public void CreatePool<T>(int autoCreateSize = 4, int allocGran = 4) where T: ISmartObj
    {            
        Type classT = typeof(T);
        CreatePool(classT, autoCreateSize, allocGran);
    }

	public void CreatePool(Type classT, int autoCreateSize = 4, int allocGran = 4)
    {
        if (classT != null && autoCreateSize > 0)
        {
            PoolHandle handle = null;
            if (_objectPool.ContainsKey(classT) == false)
            {
                handle = new PoolHandle(classT, autoCreateSize, allocGran);
                _objectPool.Add(classT, handle);
            }
        }
    }

	public T Fetch<T> (int autoCreateSize = 4, int allocGran = 4) where T: ISmartObj
    {
        Type classT = typeof(T);
        if (classT != null)
        {
            PoolHandle handle = null;
            if (_objectPool.ContainsKey(classT))
            {
                handle = _objectPool[classT];
            }
            else
            {
				handle = new PoolHandle(classT, autoCreateSize, allocGran);
                _objectPool.Add(classT, handle);
            }

            return (T)handle.Fetch();
        }

        return default(T);
    }
}
