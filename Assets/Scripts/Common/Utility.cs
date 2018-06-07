
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

public class Utility
{
 
	public static int TimeToFrame(float time)
	{
		return (int)Math.Ceiling((double)(time / Time.fixedDeltaTime));
	}

	public static float FrameToTime(int frame)
	{
		return (float)frame * Time.fixedDeltaTime;
	}

	public static Vector3 GetGameObjectSize(GameObject obj)
	{
		Vector3 result = Vector3.zero;
		if (obj.GetComponent<Renderer>() != null)
		{
			result = obj.GetComponent<Renderer>().bounds.size;
		}
		Renderer[] componentsInChildren = obj.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Renderer renderer = componentsInChildren[i];
			Vector3 size = renderer.bounds.size;
			result.x = Math.Max(result.x, size.x);
			result.y = Math.Max(result.y, size.y);
			result.z = Math.Max(result.z, size.z);
		}
		return result;
	}

	public static GameObject FindChild(GameObject p, string path)
	{
		if (p != null)
		{
			Transform transform = p.transform.Find(path);
			return (!(transform != null)) ? null : transform.gameObject;
		}
		return null;
	}

	public static GameObject FindChildSafe(GameObject p, string path)
	{
		if (p)
		{
			Transform transform = p.transform.Find(path);
			if (transform)
			{
				return transform.gameObject;
			}
		}
		return null;
	}

	public static T GetComponetInChild<T>(GameObject p, string path) where T : MonoBehaviour
	{
		if (p == null || p.transform == null)
		{
			return (T)((object)null);
		}
		Transform transform = p.transform.Find(path);
		if (transform == null)
		{
			return (T)((object)null);
		}
		return transform.GetComponent<T>();
	}

    public static T AddComponetInChildIfNotExist<T>(GameObject p, string path) where T : MonoBehaviour
    {
        if (p == null || p.transform == null)
        {
            return (T)((object)null);
        }
        Transform transform = p.transform.Find(path);
        if (transform == null)
        {
            return (T)((object)null);
        }
        T component = transform.GetComponent<T>();
        if (component != null)
        {
            return component;
        }
        return transform.gameObject.AddComponent<T>();
    }

	public static GameObject FindChildByName(GameObject root, string childpath)
	{
		GameObject result = null;
		string[] array = childpath.Split(new char[]
		{
			'/'
		});
		GameObject gameObject = root;
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i];
			bool flag = false;
			IEnumerator enumerator = gameObject.transform.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					Transform transform = (Transform)enumerator.Current;
					if (transform.gameObject.name == text)
					{
						gameObject = transform.gameObject;
						flag = true;
						break;
					}
				}
			}
			finally
			{
				IDisposable disposable = enumerator as IDisposable;
				if (disposable != null)
				{
					disposable.Dispose();
				}
			}
			if (!flag)
			{
				break;
			}
		}
		if (gameObject != root)
		{
			result = gameObject;
		}
		return result;
	}


	public static Type GetType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}
		Type type = Type.GetType(typeName);
		if (type != null)
		{
			return type;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Assembly assembly = assemblies[i];
			type = assembly.GetType(typeName);
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}



    public delegate int InsertComparsionFunc<T>(T atom, T curr, T last);
    public static bool LinkedListInsert<T> (LinkedList<T> list, T atom, InsertComparsionFunc<T> func)
    {
        if (list != null && atom != null)
        {
            if (list.Count == 0)
            {
                list.AddFirst(atom);
                return true;
            }

            LinkedListNode<T> node = list.First, nodePrev = default(LinkedListNode<T>);
            while (node != null && node.Value != null)
            {
                T curr = node.Value;
                T last = nodePrev != null ? nodePrev.Value : default(T);
                int result = func(atom, curr, last);
                if (result == -1)
                {
                    list.AddBefore(node, atom);
                    return true;
                }
                else if (result == 1)
                {
                    list.AddAfter(nodePrev, atom);
                    return true;
                }
                else if (result == 2) // the atom has already in the list
                {
                    return false;
                }
                nodePrev = node;
                node = node.Next;
            }

            if (func(atom, nodePrev.Value, default(T)) != 2)
            {
                // add tail
                list.AddLast(atom);
                return true;
            }
        }

        return false;
    }
}

public static class RawDataWriter
{
    public static void PutInt(byte[] buffer, int offset, int value)
    {
        PutUInt(buffer, offset, (uint)value);
    }

    public static void PutUInt(byte[] buffer, int offset, uint value)
    {
        if (BitConverter.IsLittleEndian)
        {
            for (int i = offset; i < offset + sizeof(uint); i++)
            {
                buffer[i] = (byte)(value >> (i - offset) * 8);
            }
        }
        else
        {
            for (int i = offset; i < offset + sizeof(uint); i++)
            {
                buffer[sizeof(uint) - 1 - i] = (byte)(value >> (i - offset) * 8);
            }
        }
    }

    public static void PutShort(byte[] buffer, int offset, short value)
    {
        PutUShort(buffer, offset, (ushort)value);
    }

    public static void PutUShort(byte[] buffer, int offset, ushort value)
    {
        if (BitConverter.IsLittleEndian)
        {
            for (int i = offset; i < offset + sizeof(ushort); i++)
            {
                buffer[i] = (byte)(value >> (i - offset) * 8);
            }
        }
        else
        {
            for (int i = offset; i < offset + sizeof(ushort); i++)
            {
                buffer[sizeof(ushort) - 1 - i] = (byte)(value >> (i - offset) * 8);
            }
        }
    }

    public static void PutLong(byte[] buffer, int offset, long value)
    {
        PutULong(buffer, offset, (ulong)value);
    }

    public static void PutULong(byte[] buffer, int offset, ulong value)
    {
        if (BitConverter.IsLittleEndian)
        {
            for (int i = offset; i < offset + sizeof(ulong); i++)
            {
                buffer[i] = (byte)(value >> (i - offset) * 8);
            }
        }
        else
        {
            for (int i = offset; i < offset + sizeof(ulong); i++)
            {
                buffer[sizeof(ulong) - 1 - i] = (byte)(value >> (i - offset) * 8);
            }
        }
    }
}
