

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

unsafe
{
  Console.WriteLine(sizeof(nint));
  Console.WriteLine($"Environment.Is64BitProcess = {Environment.Is64BitProcess}");
}

var obj = new Empty();
Console.WriteLine(obj.Foo.ToString("X16"));

ObjectHeaderUtils.Dump(obj);
GC.KeepAlive(obj.GetHashCode());
ObjectHeaderUtils.Dump(obj);



public static class ObjectHeaderUtils
{
  private static readonly Func<object, nuint> ourReadHeaderFunc;

  static unsafe ObjectHeaderUtils()
  {
    var dynamicMethod = new DynamicMethod(
      "GetObjectHeader",
      MethodAttributes.Public | MethodAttributes.Static,
      CallingConventions.Standard,
      returnType: typeof(nuint),
      parameterTypes: [typeof(object)], typeof(ObjectHeaderUtils),
      skipVisibility: false);

    _ = dynamicMethod.DefineParameter(position: 0, ParameterAttributes.None, "obj");

    var il = dynamicMethod.GetILGenerator();
    var headerLocal = il.DeclareLocal(typeof(nuint));

    // nuint header = *(((byte*)obj) - sizeof(nint));
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Conv_U);
    il.Emit(OpCodes.Ldc_I4, sizeof(nint));
    il.Emit(OpCodes.Sub); // subtract `sizeof(nint)` bytes
    il.Emit(OpCodes.Ldind_I);
    il.Emit(OpCodes.Conv_U);
    il.Emit(OpCodes.Stloc, headerLocal);

    // GC.KeepAlive(obj);
    var keepAliveMethodInfo = typeof(GC).GetMethod(
      name: nameof(GC.KeepAlive),
      bindingAttr: BindingFlags.Public | BindingFlags.Static,
      binder: null,
      callConvention: CallingConventions.Standard,
      types: [typeof(object)],
      modifiers: null) ?? throw new ArgumentException("Can't find GC.KeepAlive() method");

    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, keepAliveMethodInfo);

    // return header;
    il.Emit(OpCodes.Ldloc, headerLocal);
    il.Emit(OpCodes.Ret);

    ourReadHeaderFunc = (Func<object, nuint>)dynamicMethod.CreateDelegate(typeof(Func<object, nuint>));
  }

  private static unsafe nuint ReadObjectHeaderValue(object obj)
  {
    //var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
    return ourReadHeaderFunc(obj);
  }

  private const int IS_HASHCODE_BIT_NUMBER = 26;
  private const int IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER = 27;
  private const int BIT_SBLK_IS_HASHCODE = 1 << IS_HASHCODE_BIT_NUMBER;
  internal const int MASK_HASHCODE_INDEX = BIT_SBLK_IS_HASHCODE - 1;
  private const int BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX = 1 << IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER;

  public static void Dump(object obj)
  {
    var headerValue = (ulong) ReadObjectHeaderValue(obj);

    Console.WriteLine(headerValue.ToString("X16"));
    //Console.WriteLine(Convert.ToString((decimal)headerValue, toBase: 2));

    if ((headerValue & (1 << IS_HASHCODE_BIT_NUMBER)) != 0)
    {
      Console.WriteLine("IS_HASHCODE_BIT_NUMBER!");
    }

    if ((headerValue & (1 << IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER)) != 0)
    {
      Console.WriteLine("IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER!");
    }
  }

  [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
  private static void NoOp<T>(T ignored) { }
}


class Empty
{
  public ulong Foo = 0xDEADBEEF;
}