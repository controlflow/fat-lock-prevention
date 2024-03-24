using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Runtime;

public static class ObjectHeaderUtils
{
  private static readonly Func<object, uint> ourReadHeaderFunc;

  static ObjectHeaderUtils()
  {
    var dynamicMethod = new DynamicMethod(
      nameof(ReadObjectHeaderValue) + "Impl",
      MethodAttributes.Public | MethodAttributes.Static,
      CallingConventions.Standard,
      returnType: typeof(uint),
      parameterTypes: [typeof(object)], typeof(ObjectHeaderUtils),
      skipVisibility: false);

    _ = dynamicMethod.DefineParameter(position: 0, ParameterAttributes.None, "obj");

    var il = dynamicMethod.GetILGenerator();
    var headerLocal = il.DeclareLocal(typeof(uint));

    // uint header = *((uint*) ((byte*)obj) - sizeof(nint)));
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Conv_U);
    il.Emit(OpCodes.Ldc_I4, IntPtr.Size);
    il.Emit(OpCodes.Sub); // subtract `sizeof(nint)` bytes

    //il.Emit(OpCodes.Ldind_I);
    //il.Emit(OpCodes.Conv_U);
    //il.Emit(OpCodes.Shr_Un, 32);
    //il.Emit(OpCodes.Conv_I4);
    il.Emit(OpCodes.Stloc, headerLocal);

    // GC.KeepAlive(obj); - keep it safe
    var keepAliveMethodInfo = typeof(GC).GetMethod(
      name: nameof(GC.KeepAlive),
      bindingAttr: BindingFlags.Public | BindingFlags.Static,
      binder: null,
      callConvention: CallingConventions.Standard,
      types: [typeof(object)],
      modifiers: null) ?? throw new ArgumentException("Can't find GC.KeepAlive(object) method");
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, keepAliveMethodInfo);

    // return header;
    il.Emit(OpCodes.Ldloc, headerLocal);
    il.Emit(OpCodes.Ret);

    ourReadHeaderFunc = (Func<object, uint>)dynamicMethod.CreateDelegate(typeof(Func<object, uint>));
  }

  private static uint ReadObjectHeaderValue(object obj) => ourReadHeaderFunc(obj);

  private const int IS_HASHCODE_BIT_NUMBER = 26;
  private const int IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER = 27;
  private const int BIT_SBLK_IS_HASHCODE = 1 << IS_HASHCODE_BIT_NUMBER;
  private const int MASK_HASHCODE_INDEX = BIT_SBLK_IS_HASHCODE - 1;
  private const int BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX = 1 << IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER;

  public static void Dump(object obj)
  {
    var headerValue = ReadObjectHeaderValue(obj);

    var hexValue = headerValue.ToString(IntPtr.Size == 4 ? "X8" : "X16");
    Console.WriteLine($"Object header: 0x{hexValue.SeparateWith('_', IntPtr.Size)}");
    var binValue = Convert.ToString(headerValue, 2).PadLeft(IntPtr.Size * 8, '0');
    Console.WriteLine($"               {binValue.SeparateWith('_', IntPtr.Size * 2)}");

    if ((headerValue & BIT_SBLK_IS_HASHCODE) != 0)
    {
      Console.WriteLine("IS_HASHCODE_BIT_NUMBER flag set (thin locking, hash stored inline)");

      var hashcode = headerValue & MASK_HASHCODE_INDEX;
      Console.WriteLine($"Object.GetHashCode() value: {hashcode}");
    }
    else if ((headerValue & BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX) != 0)
    {
      Console.WriteLine("IS_HASH_OR_SYNCBLKINDEX_BIT_NUMBER flag set (sync block entry with fat lock and hash value)");

      var syncBlockIndex = headerValue & MASK_HASHCODE_INDEX;
      Console.WriteLine($"Sync block index #{syncBlockIndex}");
    }
    else
    {
      Console.WriteLine("No flags set (thin locking, no hashing has been performed before)");
    }

    Console.WriteLine();
  }

  public static bool HasSyncBlock(object obj)
  {
    var headerValue = ReadObjectHeaderValue(obj);
    return (headerValue & (BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX | BIT_SBLK_IS_HASHCODE)) == BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX;
  }

  [MethodImpl(MethodImplOptions.NoOptimization)]
  public static void AllocateSyncBlock(object obj)
  {
    int hashCode;
    lock (obj) { hashCode = obj.GetHashCode(); } // magic combination

    GC.KeepAlive(hashCode); // use value to avoid optimizations

    if (!HasSyncBlock(obj))
      throw new ArgumentException("Failed to allocate sync block!");
  }

  private static string SeparateWith(this string s, char separator, int eachChars)
  {
    var sb = new StringBuilder(s.Length + s.Length / eachChars);

    for (var index = 0; index < s.Length; index++)
    {
      if (index % eachChars == 0 && index != 0) sb.Append(separator);

      sb.Append(s[index]);
    }

    return sb.ToString();
  }
}