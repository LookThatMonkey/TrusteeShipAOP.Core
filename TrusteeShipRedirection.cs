using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TrusteeShipAOP.Core
{
    public static class TrusteeShipRedirection
    {
        internal static void HookMethod(MethodInfo sourceMethod, MethodInfo destinationMethod)
        {
            if (sourceMethod.IsVirtual)
            {
                HookVirtualMethod(sourceMethod, destinationMethod);
                return;
            }

            HookMethod(sourceMethod.MethodHandle, destinationMethod.MethodHandle);
        }

        internal static void HookMethod(RuntimeMethodHandle sourceMethod, RuntimeMethodHandle destinationMethod)
        {
            RuntimeHelpers.PrepareMethod(sourceMethod);
            RuntimeHelpers.PrepareMethod(destinationMethod);
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* inj = (int*)destinationMethod.Value.ToPointer() + 2;
                    int* tar = (int*)sourceMethod.Value.ToPointer() + 2;
                    *tar = *inj;
                }
                else
                {
                    long* inj = (long*)destinationMethod.Value.ToPointer() + 1;
                    long* tar = (long*)sourceMethod.Value.ToPointer() + 1;
                    *tar = *inj;
                }
            }
        }

        internal static void PrepareMethods(List<RuntimeMethodHandle> runtimeMethodHandles)
        {
            foreach (RuntimeMethodHandle runtimeMethodHandle in runtimeMethodHandles)
            {
                RuntimeHelpers.PrepareMethod(runtimeMethodHandle);
            }
        }

        internal static void HookConstructor2(ConstructorInfo sourceConstructorInfo, RuntimeMethodHandle destinationMethod)
        {
            RuntimeHelpers.RunClassConstructor(sourceConstructorInfo.DeclaringType.TypeHandle);
            RuntimeHelpers.PrepareMethod(sourceConstructorInfo.MethodHandle);
            RuntimeHelpers.PrepareMethod(destinationMethod);
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* inj = (int*)destinationMethod.Value.ToPointer() + 2;
                    int* tar = (int*)sourceConstructorInfo.MethodHandle.Value.ToPointer() + 2;
                    *tar = *inj;
                }
                else
                {
                    long* inj = (long*)destinationMethod.Value.ToPointer() + 1;
                    long* tar = (long*)sourceConstructorInfo.MethodHandle.Value.ToPointer() + 1;
                    *tar = *inj;
                }
            }
        }

        internal static void HookConstructor(RuntimeMethodHandle sourceMethod, ConstructorInfo destinationConstructorInfo)
        {
            RuntimeHelpers.RunClassConstructor(destinationConstructorInfo.DeclaringType.TypeHandle);
            RuntimeHelpers.PrepareMethod(sourceMethod);
            RuntimeHelpers.PrepareMethod(destinationConstructorInfo.MethodHandle);
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* inj = (int*)destinationConstructorInfo.MethodHandle.Value.ToPointer() + 2;
                    int* tar = (int*)sourceMethod.Value.ToPointer() + 2;
                    *tar = *inj;
                }
                else
                {
                    long* inj = (long*)destinationConstructorInfo.MethodHandle.Value.ToPointer() + 1;
                    long* tar = (long*)sourceMethod.Value.ToPointer() + 1;
                    *tar = *inj;
                }
            }
        }

        private static void HookVirtualMethod(IntPtr sourceMethodIntPtr, IntPtr sourceMethodTypeHandle, IntPtr destinationMethodIntPtr, IntPtr destinationMethodTypeHandle)
        {
            unsafe
            {
                UInt64* methodDesc = (UInt64*)(sourceMethodIntPtr.ToPointer());
                int index = (int)(((*methodDesc) >> 32) & 0xFF);
                if (IntPtr.Size == 4)
                {
                    uint* classStart = (uint*)sourceMethodTypeHandle.ToPointer();
                    classStart += 10;
                    classStart = (uint*)*classStart;
                    uint* tar = classStart + index;
                    uint* inj = (uint*)destinationMethodIntPtr.ToPointer() + 2;
                    *tar = *inj;
                }
                else
                {
                    ulong* classStart = (ulong*)sourceMethodTypeHandle.ToPointer();
                    classStart += 8;
                    classStart = (ulong*)*classStart;
                    ulong* tar = classStart + index;
                    ulong* inj = (ulong*)destinationMethodIntPtr.ToPointer() + 1;
                    *tar = *inj;
                }
            }
        }

        private static void HookVirtualMethod(MethodInfo sourceMethod, MethodInfo destinationMethod)
        {
            unsafe
            {
                UInt64* methodDesc = (UInt64*)(sourceMethod.MethodHandle.Value.ToPointer());
                int index = (int)(((*methodDesc) >> 32) & 0xFF);
                if (IntPtr.Size == 4)
                {
                    uint* classStart = (uint*)sourceMethod.DeclaringType.TypeHandle.Value.ToPointer();
                    classStart += 10;
                    classStart = (uint*)*classStart;
                    uint* tar = classStart + index;
                    uint* inj = (uint*)destinationMethod.MethodHandle.Value.ToPointer() + 2;
                    *tar = *inj;
                }
                else
                {
                    ulong* classStart = (ulong*)sourceMethod.DeclaringType.TypeHandle.Value.ToPointer();
                    classStart += 8;
                    classStart = (ulong*)*classStart;
                    ulong* tar = classStart + index;
                    ulong* inj = (ulong*)destinationMethod.MethodHandle.Value.ToPointer() + 1;
                    *tar = *inj;
                }
            }
        }

        private static Action<T, object> EmitSetter<T>(string propertyName)
        {
            var type = typeof(T);
            var dynamicMethod = new DynamicMethod("EmitCallable", null, new[] { type, typeof(object) }, type.Module);
            var iLGenerator = dynamicMethod.GetILGenerator();

            var callMethod = type.GetMethod("set_" + propertyName, BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public);
            var parameterInfo = callMethod.GetParameters()[0];
            var local = iLGenerator.DeclareLocal(parameterInfo.ParameterType, true);

            iLGenerator.Emit(OpCodes.Ldarg_1);
            if (parameterInfo.ParameterType.IsValueType)
            {
                // 如果是值类型，拆箱
                iLGenerator.Emit(OpCodes.Unbox_Any, parameterInfo.ParameterType);
            }
            else
            {
                // 如果是引用类型，转换
                iLGenerator.Emit(OpCodes.Castclass, parameterInfo.ParameterType);
            }

            iLGenerator.Emit(OpCodes.Stloc, local);
            iLGenerator.Emit(OpCodes.Ldarg_0);
            iLGenerator.Emit(OpCodes.Ldloc, local);

            iLGenerator.EmitCall(OpCodes.Callvirt, callMethod, null);
            iLGenerator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate(typeof(Action<T, object>)) as Action<T, object>;
        }

        public static void EmitSetter<T>(this T entity, string propertyName, object value)
        {
            EmitSetter<T>(propertyName).Invoke(entity, value);
        }

        public static void EmitSetter(object entity, string propertyName, object value)
        {
            entity.EmitSetter(propertyName, value);
        }

        public static void SetFieldValue(object entity, object value, string propertyName)
        {
            FieldInfo fieldInfo = entity.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            fieldInfo.SetValue(entity, value);
        }

        public static object GetFieldValue(object entity, string propertyName)
        {
            BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo fieldInfo = entity.GetType().GetField(propertyName, bindingFlags);
            return fieldInfo.GetValue(entity);
        }

        public static MethodInfo GetMethod(object entity, string methodName)
        {
            return entity.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
    }
}
