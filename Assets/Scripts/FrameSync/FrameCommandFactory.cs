using System;
using System.Collections.Generic;
using System.Reflection;

namespace TurnSyncModule
{
	public static class TurnCommandFactory
	{
        public delegate ITurnCommand CreatorDelegate();

		public static CreatorDelegate[] s_CommandCreator = null;
        public static Dictionary<Type, int> s_CommandTypeDef = new Dictionary<Type,int>();

        public static CreatorDelegate[] s_CommandC2SCreator = null;
        public static Dictionary<Type, int> s_CommandC2STypeDef = new Dictionary<Type,int>();

        private static CreatorDelegate GetCreator(Type hostClass)
        {
            MethodInfo[] methods = hostClass.GetMethods();
            int index = 0;
            Type creatorT = typeof(TurnCommandCreatorAttribute);

            while (methods != null && index < methods.Length)
            {
                MethodInfo methodInfo = methods[index++];
                if (methodInfo.IsStatic)
                {
                    object[] attrs = methodInfo.GetCustomAttributes(creatorT, true);
                    for (int i = 0; i < attrs.Length; i++)
                    {
                        if (attrs[i].GetType() == creatorT)
                        {
                            return (CreatorDelegate) Delegate.CreateDelegate(typeof(CreatorDelegate), methodInfo);
                        }
                    }
                }                
            }

            return null;
        }
       
        private static void RegisterCommands(int count, Type attrT, Assembly assembly, ref Dictionary<Type, int> defineTbl, out CreatorDelegate[] creatorList)
        {
            creatorList = new CreatorDelegate[count];            
            Type[] types = assembly.GetTypes();
            int idx = 0;
            while (types != null && idx < types.Length)
            {
                Type type = types[idx++];
                object[] customAttrs = type.GetCustomAttributes(attrT, true);
                for (int i = 0; i < customAttrs.Length; i++)
                {
                    TurnClassAttribute attr = customAttrs[i] as TurnClassAttribute;
                    if (attr != null)
                    {
                        CreatorDelegate creator = GetCreator(type);
                        if (creator != null)
                        {
                            creatorList[attr.CreatorID] = creator;
                            defineTbl.Add(type, attr.CreatorID);

                            break;
                        }
                    }
                }
            }
        }

		public static void PrepareRegisterCommand(Assembly assembly)
        {
            
        }

		public static TurnCommand<T> CreateTurnCommand<T>() where T : struct, ICommand
        {
			return new TurnCommand<T>
			{
				cmdType = (byte)s_CommandTypeDef[typeof(T)],
				cmdData = default(T),
                syncType = 1          
			};
		}

        public static TurnCommand<T> CreateC2STurnCommand<T> () where T : struct, ICommand
        {
            return new TurnCommand<T>
            {
                cmdType = (byte)s_CommandC2STypeDef[typeof(T)],
                cmdData = default(T),
                syncType = 2
            };
        }
	}
}
