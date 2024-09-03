using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrusteeShipAOP.Core.Attribute;

namespace TrusteeShipAOP.Core
{
    public static class Environment
    {
        public static TSEntrty TSEntrty { get; } = new TSEntrty();
        private static ShareMem _memDB = new ShareMem();
        private static List<string> _hasAssemblyNames = new List<string>();
        private static readonly byte[] _trueByteData = {
                            84, 114, 117, 115, 116,
                            101, 101, 83, 104, 105,
                            112, 65, 79, 80, 46,
                            67, 111, 114, 101, 58,
                            32, 84, 114, 117, 101,
                            84, 114, 117, 115, 116,
                            101, 101, 83, 104, 105,
                            112, 65, 79, 80, 46,
                            67, 111, 114, 101};
        private static readonly byte[] _falseByteData = {
                            84, 114, 117, 115, 116,
                            101, 101, 83, 104, 105,
                            112, 65, 79, 80, 46,
                            67, 111, 114, 101, 58,
                            32, 84, 114, 117, 101,
                            84, 114, 117, 115, 116,
                            101, 101, 83, 104, 105,
                            112, 65, 79, 80, 46,
                            67, 111, 114, 101};
        private static bool _initialsuccess = false;
        public static event EventHandler<AssemblyLoadCompletedEventArgs> AssemblyLoadCompleted;

        public static bool Initial(out string msg)
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
            var stacktrace = new StackTrace();
            Assembly mainAssembly = stacktrace.GetFrame(1).GetMethod().DeclaringType.Assembly;
            if (mainAssembly.EntryPoint == stacktrace.GetFrame(1).GetMethod())
            {
                _initialsuccess = true;
                _memDB.Init("TrusteeShipAOP.Core", _trueByteData.Length);
                _memDB.Write(_trueByteData, 0, _trueByteData.Length);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    RegisterAssembly(assembly, out msg);
                }

                msg = "";
                return true;
            }
            
            msg = $"";
            return false;
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args != null && args.LoadedAssembly != null)
            {
                if (args.LoadedAssembly.GetReferencedAssemblies().Count(c => c.FullName == typeof(TrusteeShipAOP.Core.Environment).Assembly.FullName) > 0)
                {
                    RegisterAssembly(args.LoadedAssembly, out _);
                }
            }
        }

        public static bool RegisterAssembly(Assembly assembly, out string meg)
        {
            _initialsuccess = true;
            if (_hasAssemblyNames.Contains(assembly.FullName))
            {
                meg = "已经加载";
                return false;
            }

            string key = Guid.NewGuid().ToString();
            _hasAssemblyNames.Add(assembly.FullName);
            _hasAssemblyNames.Add($"{key}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (Assembly.GetExecutingAssembly().GetName().Name == assembly.GetName().Name)
            {
                meg = "";
                return true;
            }

            if (!_initialsuccess)
            {
                byte[] trueByteData = new byte[_trueByteData.Length];
                _memDB.Init("TrusteeShipAOP.Core", trueByteData.Length);
                int intRet = _memDB.Read(ref trueByteData, 0, trueByteData.Length);
                if (!_trueByteData.SequenceEqual(trueByteData))
                {
                    var stacktrace = new StackTrace();
                    meg = $"请在{stacktrace.GetFrame(1).GetMethod().DeclaringType.FullName}.{stacktrace.GetFrames()[stacktrace.FrameCount - 1].GetMethod().Name}处进行第一次注册！";
                    return false;
                }
            }

            List<MethodProxyTypeFullNameEntity> proxyMethods = new List<MethodProxyTypeFullNameEntity>();
            if (assembly.ManifestModule.ScopeName.StartsWith(key))
            {
                meg = "";
                return true;
            }
            
            string _dllName = key;
            AssemblyEntetry am = CreateDll(_dllName);
            List<List<MethodProxyTypeFullNameEntity>> methodProxyTypeFullNameEntities = InitialAssembly(assembly, am);
            proxyMethods.AddRange(methodProxyTypeFullNameEntities[0]);
            if (am.AssemblyBuilder != null)
            {
                if (methodProxyTypeFullNameEntities.Count > 1 && methodProxyTypeFullNameEntities[1].Count > 0)
                {
                    foreach (MethodProxyTypeFullNameEntity proxyMethod in methodProxyTypeFullNameEntities[1])
                    {
                        MethodInfo proxyCopyMethodInfo = am.AssemblyBuilder.GetType(proxyMethod.ProxyTypeFullName).GetMethod(
                                    "ZGLCreateProxyCopy",
                                    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic,
                                    null,
                                    proxyMethod.ConstructorInfo.GetParameters().Select(s => s.ParameterType).ToArray(),
                                    null);
                        TrusteeShipRedirection.HookMethod(proxyCopyMethodInfo.MethodHandle, proxyMethod.ConstructorInfo.MethodHandle);
                        MethodInfo proxyMethodInfo = am.AssemblyBuilder.GetType(proxyMethod.ProxyTypeFullName).GetMethod(
                                    "ZGLCreateProxy",
                                    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic,
                                    null,
                                    proxyMethod.ConstructorInfo.GetParameters().Select(s => s.ParameterType).ToArray(),
                                    null);
                        TrusteeShipRedirection.HookMethod(proxyMethod.ConstructorInfo.MethodHandle, proxyMethodInfo.MethodHandle);
                    }
                }

                foreach (MethodProxyTypeFullNameEntity proxyMethod in proxyMethods)
                {
                    MethodInfo proxyCopyMethodInfo = am.AssemblyBuilder.GetType(proxyMethod.ProxyTypeFullName).GetMethod(
                                proxyMethod.MethodInfo.Name.Split('`')[0] + "ProxyCopy",
                                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic,
                                null,
                                proxyMethod.MethodInfo.GetParameters().Select(s => s.ParameterType).ToArray(),
                                null);
                    TrusteeShipRedirection.HookMethod(proxyCopyMethodInfo, proxyMethod.MethodInfo);
                    MethodInfo proxyMethodInfo = am.AssemblyBuilder.GetType(proxyMethod.ProxyTypeFullName).GetMethod(
                                proxyMethod.MethodInfo.Name.Split('`')[0] + "Proxy",
                                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic,
                                null,
                                proxyMethod.MethodInfo.GetParameters().Select(s => s.ParameterType).ToArray(),
                                null);
                    TrusteeShipRedirection.HookMethod(proxyMethod.MethodInfo, proxyMethodInfo);
                }
            }

            meg = "";
            AssemblyLoadCompleted?.Invoke(assembly, new AssemblyLoadCompletedEventArgs() { AssemblyName = assembly.FullName });
            return true;

        }

        private static AssemblyEntetry CreateDll(string _dllName)
        {
            List<CustomAttributeBuilder> assemblyAttributes = new List<CustomAttributeBuilder>();
            Type[] ctorParams = new Type[] { typeof(DebuggableAttribute.DebuggingModes) };
            ConstructorInfo classCtorInfo = typeof(DebuggableAttribute).GetConstructor(ctorParams);
            CustomAttributeBuilder debugBuilder = new CustomAttributeBuilder(classCtorInfo, new object[] {
                DebuggableAttribute.DebuggingModes.Default
                | DebuggableAttribute.DebuggingModes.DisableOptimizations
                | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints
                | DebuggableAttribute.DebuggingModes.EnableEditAndContinue });
            assemblyAttributes.Add(debugBuilder);
            ctorParams = new Type[] { typeof(int) };
            classCtorInfo = typeof(CompilationRelaxationsAttribute).GetConstructor(ctorParams);
            CustomAttributeBuilder compilationRelaxationsBuilder = new CustomAttributeBuilder(classCtorInfo, new object[] { 8 });
            assemblyAttributes.Add(compilationRelaxationsBuilder);
            ctorParams = new Type[] { };
            classCtorInfo = typeof(RuntimeCompatibilityAttribute).GetConstructor(ctorParams);
            PropertyInfo propertyInfo = typeof(RuntimeCompatibilityAttribute).GetProperty("WrapNonExceptionThrows");
            CustomAttributeBuilder runtimeCompatibilityBuilder = new CustomAttributeBuilder(classCtorInfo, new object[] { }, new PropertyInfo[] { propertyInfo }, new object[] { true });
            assemblyAttributes.Add(runtimeCompatibilityBuilder);
            AssemblyName DemoName = new AssemblyName(_dllName);
            AssemblyBuilderAccess assemblyBuilderAccess = AssemblyBuilderAccess.RunAndSave;
            AssemblyBuilder _assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(DemoName, assemblyBuilderAccess, assemblyAttributes, SecurityContextSource.CurrentAssembly);
            ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule($"{_dllName}.dll");
            return new AssemblyEntetry() { AssemblyBuilder = _assemblyBuilder, ModuleBuilder = _moduleBuilder };
        }

        private static List<List<MethodProxyTypeFullNameEntity>> InitialAssembly(Assembly assembly, AssemblyEntetry am)
        {
            List<MethodProxyTypeFullNameEntity> vals = new List<MethodProxyTypeFullNameEntity>();
            List<MethodProxyTypeFullNameEntity> vals2 = new List<MethodProxyTypeFullNameEntity>();
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                ClassAspectAttribute classAspect = type.GetCustomAttribute<ClassAspectAttribute>();
                if (classAspect != null)
                {
                    TypeBuilder typeBuilderProxy = am.ModuleBuilder.DefineType("ZGL." + type.Name.Split('`')[0] + "Proxy", TypeAttributes.Public | TypeAttributes.BeforeFieldInit);
                    if (type.GetGenericArguments().Length > 0)
                    {
                        typeBuilderProxy.DefineGenericParameters(type.GetGenericArguments().Select(s => s.Name).ToArray());
                    }

                    ConstructorInfo[] constructorInfos = type.GetConstructors();
                    foreach (ConstructorInfo constructorInfo in constructorInfos)
                    {
                        MethodBuilder methodBuilder = typeBuilderProxy.DefineMethod("ZGLCreateProxy", constructorInfo.Attributes, type, constructorInfo.GetParameters().Select(s => s.ParameterType).ToArray());
                        MethodBuilder methodBuilderCopy = typeBuilderProxy.DefineMethod("ZGLCreateProxyCopy", constructorInfo.Attributes, type, constructorInfo.GetParameters().Select(s => s.ParameterType).ToArray());
                        ILGenerator il = methodBuilder.GetILGenerator();
                        ILGenerator ilCopy = methodBuilderCopy.GetILGenerator();
                        ilCopy.Emit(OpCodes.Nop);
                        ilCopy.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(Type.EmptyTypes));
                        ilCopy.Emit(OpCodes.Throw);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, typeof(Core.Environment).GetMethod("AddEntrty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                        il.Emit(OpCodes.Ldarg_0);
                        for (int i = 1; i <= constructorInfo.GetParameters().Length; i++)
                        {
                            il.Emit(OpCodes.Ldarg, i);
                        }

                        il.Emit(OpCodes.Callvirt, methodBuilderCopy);
                        il.Emit(OpCodes.Ret);
                        vals2.Add(new MethodProxyTypeFullNameEntity(constructorInfo, "ZGL." + type.Name.Split('`')[0] + "Proxy"));
                    }

                    FieldInfo[] fieldInfos = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (FieldInfo fieldInfo in fieldInfos)
                    {
                        if (fieldInfo.IsPrivate)
                        {
                            //FieldBuilder fieldBuilder = _typeBuilderProxy.DefineField(fieldInfo.Name + "FieldInfo", typeof(FieldInfo), FieldAttributes.Private);
                            //fieldBuilder.SetValue
                        }
                    }
                    EventInfo[] eventInfos = type.GetEvents(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public);
                    foreach (EventInfo eventInfo in eventInfos)
                    {
                        IEnumerable<MPAspectAttribute> methodAspectAttributes = eventInfo.GetCustomAttributes<MPAspectAttribute>();
                        if (methodAspectAttributes != null && methodAspectAttributes.Count() > 0)
                        {
                            if (eventInfo.AddMethod != null)
                            {
                                MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, eventInfo.AddMethod);
                            }

                            if (eventInfo.RemoveMethod != null)
                            {
                                MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, eventInfo.RemoveMethod);
                            }

                            if (eventInfo.RaiseMethod != null)
                            {
                                MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, eventInfo.RaiseMethod);
                            }
                        }
                    }

                    PropertyInfo[] propertyInfos = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (PropertyInfo propertyInfo in propertyInfos)
                    {
                        IEnumerable<MPAspectAttribute> methodAspectAttributes = propertyInfo.GetCustomAttributes<MPAspectAttribute>();
                        if (methodAspectAttributes != null && methodAspectAttributes.Count() > 0)
                        {
                            if (propertyInfo.GetMethod != null)
                            {
                                MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, propertyInfo.GetMethod, false, 1);
                            }

                            if (propertyInfo.SetMethod != null)
                            {
                                MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, propertyInfo.SetMethod, true, 2);
                            }
                        }
                    }

                    MethodInfo[] methodInfos = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (MethodInfo methodInfo in methodInfos)
                    {
                        IEnumerable<MPAspectAttribute> methodAspectAttributes = methodInfo.GetCustomAttributes<MPAspectAttribute>();
                        if (methodAspectAttributes != null && methodAspectAttributes.Count() > 0)
                        {
                            MethodHandle(vals, methodAspectAttributes, type, typeBuilderProxy, methodInfo);
                        }
                    }
                    
                    typeBuilderProxy.CreateType();
                }
            }

            return new List<List<MethodProxyTypeFullNameEntity>>() { vals, vals2 };
        }

        private static void MethodHandle(List<MethodProxyTypeFullNameEntity> vals, IEnumerable<MPAspectAttribute> methodAspectAttributes, Type type, TypeBuilder typeBuilderProxy, MethodInfo methodInfo, bool validating = false, int propertyMethodType = 0)
        {
            MethodBuilder methodBuilder = typeBuilderProxy.DefineMethod(methodInfo.Name.Split('`')[0] + "Proxy", methodInfo.Attributes, methodInfo.ReturnType, methodInfo.GetParameters().Select(s => s.ParameterType).ToArray());
            MethodBuilder methodBuilderCopy = typeBuilderProxy.DefineMethod(methodInfo.Name.Split('`')[0] + "ProxyCopy", methodInfo.Attributes, methodInfo.ReturnType, methodInfo.GetParameters().Select(s => s.ParameterType).ToArray());
            ILGenerator il = methodBuilder.GetILGenerator();
            ILGenerator ilCopy = methodBuilderCopy.GetILGenerator();
            foreach (LocalVariableInfo localVariableInfo in methodInfo.GetMethodBody().LocalVariables)
            {
                il.DeclareLocal(localVariableInfo.LocalType);
                ilCopy.DeclareLocal(localVariableInfo.LocalType);
            }

            TSIntercept.MethodAndInterceptors.Add(type.FullName + "." + methodInfo.Name + "0", new MethodAspectAttributeEntity(typeof(MPAspectAttribute).GetMethod("OnEntry"), methodAspectAttributes));
            TSIntercept.MethodAndInterceptors.Add(type.FullName + "." + methodInfo.Name + "1", new MethodAspectAttributeEntity(typeof(MPAspectAttribute).GetMethod("OnExit"), methodAspectAttributes));
            TSIntercept.MethodAndInterceptors.Add(type.FullName + "." + methodInfo.Name + "3", new MethodAspectAttributeEntity(typeof(MPAspectAttribute).GetMethod("OnExceptionFinally"), methodAspectAttributes));
            TSIntercept.MethodAndInterceptors2.Add(type.FullName + "." + methodInfo.Name + "0", new MethodAspectAttributeEntity(typeof(MPAspectAttribute).GetMethod("Validating"), methodAspectAttributes));
            TSIntercept.MethodAndInterceptors3.Add(type.FullName + "." + methodInfo.Name + "0", new MethodAspectAttributeEntity(typeof(MPAspectAttribute).GetMethod("OnException"), methodAspectAttributes));
            //MethodBodyReader mr = new MethodBodyReader(methodInfo);
            //mr.Emit(ref ilCopy);
            //ilCopy.Emit(OpCodes.Ret);
            ilCopy.Emit(OpCodes.Nop);
            ilCopy.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(Type.EmptyTypes));
            ilCopy.Emit(OpCodes.Throw);
            System.Reflection.Emit.Label falseReturnLabel = il.DefineLabel();
            LocalBuilder objParams = il.DeclareLocal(typeof(object[]));
            int ParametersLength = methodInfo.GetParameters().Length;
            if (ParametersLength <= 0)
            {
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stloc, objParams);
            }
            else
            {
                int IsStaticIndex = methodInfo.IsStatic ? 0 : 1;
                ////数组长度入栈
                il.Emit(OpCodes.Ldc_I4, ParametersLength);
                ////生成新数组
                il.Emit(OpCodes.Newarr, typeof(object));
                ////赋值给局部数组变量
                il.Emit(OpCodes.Stloc, objParams);
                for (int j = 0; j < ParametersLength; j++)
                {
                    il.Emit(OpCodes.Ldloc, objParams);//数组入栈
                    il.Emit(OpCodes.Ldc_I4, j);//数组下标入栈
                    il.Emit(OpCodes.Ldarg, j + IsStaticIndex);//按下标加载对应的参数
                    if (methodInfo.GetParameters()[j].ParameterType.IsValueType)//参数为值类型，装箱
                    {
                        il.Emit(OpCodes.Box, methodInfo.GetParameters()[j].ParameterType);
                    }

                    il.Emit(OpCodes.Stelem_Ref);//将参数存入数组
                }
            }

            if (validating)
            {
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
                il.Emit(OpCodes.Ldstr, methodInfo.Name);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldloc, objParams);
                LocalBuilder falseReturn = il.DeclareLocal(typeof(bool));
                il.Emit(OpCodes.Ldloca_S, falseReturn);
                il.Emit(OpCodes.Ldc_I4, propertyMethodType);
                il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDoValidating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                il.Emit(OpCodes.Ldloc, falseReturn);
                il.Emit(OpCodes.Brfalse_S, falseReturnLabel);
            }

            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
            il.Emit(OpCodes.Ldstr, methodInfo.Name);
            if (methodInfo.IsSpecialName)
            {
                il.Emit(OpCodes.Ldc_I4_0);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4_1);
            }

            il.Emit(OpCodes.Ldloc, objParams);
            il.Emit(OpCodes.Ldc_I4, propertyMethodType);
            il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            il.Emit(OpCodes.Nop);
            if (methodInfo.ReturnType != typeof(void))
            {
                LocalBuilder localBuilder = il.DeclareLocal(methodInfo.ReturnType);
                //开始try块
                il.BeginExceptionBlock();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 1; i <= methodInfo.GetParameters().Length; i++)
                {
                    il.Emit(OpCodes.Ldarg, i);
                }
                il.Emit(OpCodes.Callvirt, methodBuilderCopy);
                il.Emit(OpCodes.Stloc, localBuilder);

                //开始catch块
                il.BeginCatchBlock(typeof(Exception));
                LocalBuilder exception = il.DeclareLocal(typeof(Exception));
                il.Emit(OpCodes.Stloc, exception);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
                il.Emit(OpCodes.Ldstr, methodInfo.Name);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldloc, objParams);
                il.Emit(OpCodes.Ldloc, exception);
                il.Emit(OpCodes.Ldc_I4, propertyMethodType);
                il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDoException", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

                //开始finally块
                il.BeginFinallyBlock();
                il.Emit(OpCodes.Ldc_I4_3);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
                il.Emit(OpCodes.Ldstr, methodInfo.Name);
                if (methodInfo.IsSpecialName)
                {
                    il.Emit(OpCodes.Ldc_I4_0);
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4_0);
                }

                il.Emit(OpCodes.Ldloc, objParams);
                il.Emit(OpCodes.Ldc_I4, propertyMethodType);
                il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                //结束try / Catch / finally块
                il.EndExceptionBlock();
                il.Emit(OpCodes.Ldloc, localBuilder);
            }
            else
            {
                il.BeginExceptionBlock();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 1; i <= methodInfo.GetParameters().Length; i++)
                {
                    il.Emit(OpCodes.Ldarg, i);
                }
                il.Emit(OpCodes.Callvirt, methodBuilderCopy);

                //开始catch块
                il.BeginCatchBlock(typeof(Exception));
                LocalBuilder exception = il.DeclareLocal(typeof(Exception));
                il.Emit(OpCodes.Stloc, exception);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
                il.Emit(OpCodes.Ldstr, methodInfo.Name);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldloc, objParams);
                il.Emit(OpCodes.Ldloc, exception);
                il.Emit(OpCodes.Ldc_I4, propertyMethodType);
                il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDoException", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

                //开始finally块
                il.BeginFinallyBlock();
                il.Emit(OpCodes.Ldc_I4_3);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
                il.Emit(OpCodes.Ldstr, methodInfo.Name);
                if (methodInfo.IsSpecialName)
                {
                    il.Emit(OpCodes.Ldc_I4_0);
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4_0);
                }

                il.Emit(OpCodes.Ldloc, objParams);
                il.Emit(OpCodes.Ldc_I4, propertyMethodType);
                il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                //结束try / Catch / finally块
                il.EndExceptionBlock();
            }
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, type.FullName + "." + methodInfo.Name);
            il.Emit(OpCodes.Ldstr, methodInfo.Name);
            if (methodInfo.IsSpecialName)
            {
                il.Emit(OpCodes.Ldc_I4_0);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4_0);
            }

            il.Emit(OpCodes.Ldloc, objParams);
            il.Emit(OpCodes.Ldc_I4, propertyMethodType);
            il.Emit(OpCodes.Call, typeof(TSIntercept).GetMethod("OnDo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            il.MarkLabel(falseReturnLabel);
            il.Emit(OpCodes.Ret);

            vals.Add(new MethodProxyTypeFullNameEntity(methodInfo, "ZGL." + type.Name.Split('`')[0] + "Proxy"));
        }
        
        /// <summary> 
        /// 解密数据 
        /// </summary> 
        /// <param name="Text"></param> 
        /// <param name="sKey"></param> 
        /// <returns></returns> 
        private static string Decrypt(string Text, string sKey)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            int len = Text.Length / 2;
            byte[] inputByteArray = new byte[len];
            int x, i;
            for (x = 0; x < len; x++)
            {
                i = Convert.ToInt32(Text.Substring(x * 2, 2), 16);
                inputByteArray[x] = (byte)i;
            }
            des.Key = Encoding.UTF8.GetBytes(sKey);
            des.IV = Encoding.UTF8.GetBytes(sKey);
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public static void AddEntrty(object obj)
        {
            TSEntrty.Add(obj);
        }

        public static void RemoveEntrty(object obj)
        {
            TSEntrty.Remove(obj);
        }

        internal class EncryptEntity
        {
            internal string Value { get; set; }
            internal string key { get; set; }
        }
    }
    public static class TSIntercept
    {
        internal static Dictionary<string, MethodAspectAttributeEntity> MethodAndInterceptors = new Dictionary<string, MethodAspectAttributeEntity>();
        internal static Dictionary<string, MethodAspectAttributeEntity> MethodAndInterceptors2 = new Dictionary<string, MethodAspectAttributeEntity>();
        internal static Dictionary<string, MethodAspectAttributeEntity> MethodAndInterceptors3 = new Dictionary<string, MethodAspectAttributeEntity>();
        public static void OnDo(int optionKey, object sender, string fullName, string Name, int type, object[] objParams, int PropertyMethodType)
        {
            if (MethodAndInterceptors.ContainsKey(fullName + optionKey))
            {
                foreach (MPAspectAttribute aspect in MethodAndInterceptors[fullName + optionKey].Aspects)
                {
                    MethodAndInterceptors[fullName + optionKey].Method.Invoke(aspect, new object[] { sender,
                        new AspectEventArgs()
                        {
                            MethodFullName = fullName,
                            MethodName = Name,
                            IsProperty = type == 0,
                            PropertyMethodType = (PropertyMethodType)PropertyMethodType,
                            Params = objParams
                        }
                    });
                }
            }
        }

        public static void OnDoValidating(int optionKey, object sender, string fullName, string Name, int type, object[] objParams, out bool val, int PropertyMethodType)
        {
            val = true;
            if (MethodAndInterceptors2.ContainsKey(fullName + optionKey))
            {
                foreach (MPAspectAttribute aspect in MethodAndInterceptors2[fullName + optionKey].Aspects)
                {
                    val &= (bool)MethodAndInterceptors2[fullName + optionKey].Method.Invoke(aspect, new object[] { sender,
                        new AspectEventArgs()
                        {
                            MethodFullName = fullName,
                            MethodName = Name,
                            IsProperty = type == 0,
                            PropertyMethodType = (PropertyMethodType)PropertyMethodType,
                            Params = objParams
                        }
                    });
                }
            }
            else
            {
                val = true;
            }
        }

        public static void OnDoException(int optionKey, object sender, string fullName, string Name, int type, object[] objParams, Exception ex, int PropertyMethodType)
        {
            if (MethodAndInterceptors3.ContainsKey(fullName + optionKey))
            {
                foreach (MPAspectAttribute aspect in MethodAndInterceptors3[fullName + optionKey].Aspects)
                {
                    MethodAndInterceptors3[fullName + optionKey].Method.Invoke(aspect, new object[] { sender,
                        new AspectEventArgs()
                        {
                            MethodFullName = fullName,
                            MethodName = Name,
                            IsProperty = type == 0,
                            PropertyMethodType = (PropertyMethodType)PropertyMethodType,
                            Params = objParams
                        }, ex
                    });
                }
            }
        }
    }

    internal class MethodProxyTypeFullNameEntity
    {
        internal MethodProxyTypeFullNameEntity(MethodInfo methodInfo, string proxyTypeFullName)
        {
            this.MethodInfo = methodInfo;
            this.ProxyTypeFullName = proxyTypeFullName;
        }
        internal MethodProxyTypeFullNameEntity(ConstructorInfo constructorInfo, string proxyTypeFullName)
        {
            this.ConstructorInfo = constructorInfo;
            this.ProxyTypeFullName = proxyTypeFullName;
        }

        internal MethodInfo MethodInfo { get; set; }
        internal ConstructorInfo ConstructorInfo { get; set; }
        internal string ProxyTypeFullName { get; set; }
    }
    internal class MethodAspectAttributeEntity
    {
        internal MethodAspectAttributeEntity(MethodInfo method, IEnumerable<MPAspectAttribute> aspects)
        {
            this.Method = method;
            this.Aspects = aspects;
        }
        internal MethodInfo Method { get; set; }
        internal IEnumerable<MPAspectAttribute> Aspects { get; set; }
    }
    public class TSEntrty
    {
        private ConcurrentObservableCollection _entrties = new ConcurrentObservableCollection();
        public ReadOnlyCollection<object> Entrties => _entrties;

        public event EventHandler<CollectionChangedEventArgs> CollectionChanged;

        public TSEntrty()
        {
            _entrties.CollectionChanged += _entrties_CollectionChanged;
        }

        private void _entrties_CollectionChanged(object sender, CollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        public void Add(object obj)
        {
            bool? res = _entrties.Contains(obj);
            if (res is bool && !(bool)res)
                _entrties.Add(obj);
        }

        public void Add(IEnumerable<object> objs)
        {
            foreach(object obj in objs)
            {
                bool? res = _entrties.Contains(obj);
                if (res is bool && !(bool)res)
                    _entrties.Add(obj);
            }
        }

        public void Remove(object obj)
        {
            _entrties.Remove(obj);
        }

        public object this[int index]
        {
            set
            {
                _entrties[index] = value;
            }
            get
            {
                return _entrties[index];
            }
        }
    }

    class AssemblyEntetry
    {
        public AssemblyBuilder AssemblyBuilder { get; set; }
        public ModuleBuilder ModuleBuilder { get; set; }
    }

    public class ConcurrentObservableCollection
    {
        private static object _lockObject = new object();
        private List<object> _dataArrary { get; } = new List<object>();

        public event EventHandler<CollectionChangedEventArgs> CollectionChanged;


        public int Count
        {
            get
            {
                return _dataArrary.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public object this[int index]
        {
            set
            {
                _dataArrary[index] = value;
            }
            get
            {
                return _dataArrary[index];
            }
        }

        public static implicit operator ReadOnlyCollection<object>(ConcurrentObservableCollection data)
        {
            return new ReadOnlyCollection<object>(data._dataArrary);
        }

        public bool? Add(object item)
        {
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    _dataArrary.Add(item);
                    //Task.Run(() =>
                    //{
                    //    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Add, Item = item });
                    //});
                    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Add, Item = item });
                    return true;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }

        public bool? Remove(object item)
        {
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    if (_dataArrary.Remove(item))
                    {
                        //Task.Run(() =>
                        //{
                        //    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Remove, Item = item });
                        //});
                        CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Remove, Item = item });
                        return true;
                    }

                    return false;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }

        [Obsolete("此函數會影響整理運行，慎用！", false)]
        public bool? Clear()
        {
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    _dataArrary.Clear();
                    //Task.Run(() =>
                    //{
                    //    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Clear });
                    //});
                    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs() { CollectionChangedType = CollectionChangedType.Clear });
                    return true;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }

        public bool? Contains(object item)
        {
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    return _dataArrary.Contains(item);
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }

        public bool? CopyTo(object[] array, int arrayIndex)
        {
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    _dataArrary.CopyTo(array, arrayIndex);
                    return true;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }
        
        public bool? GetEnumerator(out IEnumerator enumerator)
        {
            enumerator = null;
            if (Monitor.TryEnter(_lockObject, 300))
            {
                try
                {
                    enumerator = _dataArrary.GetEnumerator();
                    return true;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }

            return null;
        }
    }
    public class AssemblyLoadCompletedEventArgs : EventArgs
    {
        public string AssemblyName { get; internal set; }
    }
    public class CollectionChangedEventArgs : EventArgs
    {
        public CollectionChangedType CollectionChangedType { get; set; } = CollectionChangedType.Add;
        public object Item { get; set; }
    }

    public class ConcurrentObservableCollection<T> : ICollection<T>
    {
        private ManualResetEvent _mreDataClear = new ManualResetEvent(true);
        private ConcurrentDictionary<string, T> _dataArrary { get; } = new ConcurrentDictionary<string, T>();

        public event EventHandler<CollectionChangedEventArgs<T>> CollectionChanged;

        public int Count
        {
            get
            {
                return _dataArrary.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(T item)
        {
            _mreDataClear.WaitOne();
            if (_dataArrary.TryAdd(Guid.NewGuid().ToString(), item))
            {
                CollectionChanged?.Invoke(this, new CollectionChangedEventArgs<T>() { CollectionChangedType = CollectionChangedType.Add, Item = item });
            }
        }

        public bool Remove(T item)
        {
            _mreDataClear.WaitOne();
            T outItem;
            if (_dataArrary.TryRemove("", out outItem))
            {
                CollectionChanged?.Invoke(this, new CollectionChangedEventArgs<T>() { CollectionChangedType = CollectionChangedType.Remove, Item = outItem });
                return true;
            }

            return false;
        }

        [Obsolete("此函數會影響整理運行，慎用！", false)]
        public void Clear()
        {
            _mreDataClear.Reset();
            _dataArrary.Clear();
            _mreDataClear.Set();
            CollectionChanged?.Invoke(this, new CollectionChangedEventArgs<T>() { CollectionChangedType = CollectionChangedType.Clear });
        }

        public bool Contains(T item)
        {
            return _dataArrary.ContainsKey(Guid.NewGuid().ToString());
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _dataArrary.Values.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _dataArrary.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dataArrary.Values.GetEnumerator();
        }
    }

    public class CollectionChangedEventArgs<T> : EventArgs
    {
        public CollectionChangedType CollectionChangedType { get; set; } = CollectionChangedType.Add;
        public T Item { get; set; }
    }

    public enum CollectionChangedType
    {
        Add,
        Remove,
        Clear
    }
}
