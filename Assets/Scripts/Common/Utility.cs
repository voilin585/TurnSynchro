
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

public class Utility
{
    private delegate bool d(int v);

	public static int TimeToTurn(float time)
	{
		return (int)Math.Ceiling((double)(time / Time.fixedDeltaTime));
	}

	public static float TurnToTime(int Turn)
	{
		return (float)Turn * Time.fixedDeltaTime;
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
