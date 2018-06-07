using System;
using UnityEngine;

namespace FrameSyncModule
{
	public class FrameRandom
	{
		private const uint maxShort = 65536u;

		private const uint multiper = 1194211693u;

		private const uint addValue = 12345u;

        private static uint nSeed = (uint)UnityEngine.Random.Range(100, 1000);//.(uint)UnityEngine.Random.Range(32767, 2147483647);

		public static uint callNum = 0u;

		public static int GetSeed()
		{
			return (int)FrameRandom.nSeed;
		}

	    public static long GetSeedLong()
	    {
            return (long)FrameRandom.nSeed;
	    }

	    public static void ResetSeed(uint seed)
		{
			nSeed = seed;
			callNum = 0u;
		}

		public static ushort Random(uint nMax)
		{
			callNum += 1u;
			nSeed = nSeed * 1194211693u + 12345u;         
            return (ushort)((nSeed >> 16) % nMax);
		}
	}
}
