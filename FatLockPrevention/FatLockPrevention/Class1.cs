using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

Console.WriteLine($"Environment.Is64BitProcess = {Environment.Is64BitProcess}");

var obj = new int[0];

Console.WriteLine("After creation:");
ObjectHeaderUtils.Dump(obj);

var hash = obj.GetHashCode();
Console.WriteLine($"After hash code ({hash}) request:");
ObjectHeaderUtils.Dump(obj);


lock (obj) { }
ObjectHeaderUtils.Dump(obj);



ObjectHeaderUtils.AllocateSyncBlock(obj);
Console.WriteLine("After allocation:");
ObjectHeaderUtils.Dump(obj);

return;

lock (obj)
{
  Console.WriteLine("Inside lock:");
  ObjectHeaderUtils.Dump(obj);

  lock (obj)
  {
    Console.WriteLine("Inside second lock:");
    ObjectHeaderUtils.Dump(obj);

    lock (obj)
    {
      Console.WriteLine("Inside third lock:");
      ObjectHeaderUtils.Dump(obj);
    }
  }
}

Console.WriteLine("After lock:");
ObjectHeaderUtils.Dump(obj);

Console.WriteLine($"obj.GetHashCode() == {obj.GetHashCode()}");
Console.WriteLine();

ObjectHeaderUtils.Dump(obj);

lock (obj)
{
  Task.Run(() =>
  {
    lock (obj)
    {
      GC.KeepAlive(obj);
    }
  });
}

ObjectHeaderUtils.Dump(obj);