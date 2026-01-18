// Dafny program the_program compiled into C#
// To recompile, you will need the libraries
//     System.Runtime.Numerics.dll System.Collections.Immutable.dll
// but the 'dotnet' tool in .NET should pick those up automatically.
// Optionally, you may want to include compiler switches like
//     /debug /nowarn:162,164,168,183,219,436,1717,1718

using System;
using System.Numerics;
using System.Collections;
[assembly: DafnyAssembly.DafnySourceAttribute(@"// dafny 4.10.0.0
// Command-line arguments: test /Users/luyihan/Desktop/AutoVeri/output/trans.dfy --no-verify --standard-libraries
// the_program

method check_greater(arr: seq<int>, number: int) returns (res: bool)
  decreases arr, number
{
  var sortedArr := arr;
  res := number > sortedArr[|sortedArr| - 1];
}

method {:test} check()
{
  var call0 := check_greater([1, 2, 3, 4, 5], 6);
  var call1 := check_greater([10, 20, 30, 40, 50], 25);
  var call2 := check_greater([-10, -20, -30, -40, -50], -5);
  var call3 := check_greater([100, 200, 300, 400, 500], 600);
  var call4 := check_greater([15, 25, 35, 45, 55], 60);
  expect call0 == true, ""expectation violation"";
  expect call1 == false, ""expectation violation"";
  expect call2 == true, ""expectation violation"";
  expect call3 == true, ""expectation violation"";
  expect call4 == true, ""expectation violation"";
}

method {:verify false} {:main} _Test__Main_(_noArgsParameter: seq<seq<char>>)
{
  var success: bool := true;
  print @""check: "";
    [[ try { ]]
{
      check()
      {
        print @""PASSED
"";
      }
    }
  [[ } recover (haltMessage) { ]]
{
      print @""FAILED
	"", haltMessage, @""
"";
      success := false;
    }[[ } ]]
  expect success, @""Test failures occurred: see above.
"";
}

import opened Seq = Std.Collections.Seq

import opened Strings = Std.Strings

import opened Math = Std.Math

import opened Power = Std.Arithmetic.Power

")]

//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

// When --include-runtime is true, this file is directly prepended
// to the output program. We have to avoid these using directives in that case
// since they can only appear before any other declarations.
// The DafnyRuntime.csproj file is the only place that ISDAFNYRUNTIMELIB is defined,
// so these are only active when building the C# DafnyRuntime.dll library.
#if ISDAFNYRUNTIMELIB
using System; // for Func
using System.Numerics;
using System.Collections;
#endif

namespace DafnyAssembly {
  [AttributeUsage(AttributeTargets.Assembly)]
  public class DafnySourceAttribute : Attribute {
    public readonly string dafnySourceText;
    public DafnySourceAttribute(string txt) { dafnySourceText = txt; }
  }
}

namespace Dafny {
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;

  // Similar to System.Text.Rune, which would be perfect to use
  // except that it isn't available in the platforms we support
  // (.NET Standard 2.0 and .NET Framework 4.5.2)
  public readonly struct Rune : IComparable, IComparable<Rune>, IEquatable<Rune> {

    private readonly uint _value;

    public Rune(int value)
      : this((uint)value) {
    }

    public Rune(uint value) {
      if (!(value < 0xD800 || (0xE000 <= value && value < 0x11_0000))) {
        throw new ArgumentException();
      }

      _value = value;
    }

    public static bool IsRune(BigInteger i) {
      return (0 <= i && i < 0xD800) || (0xE000 <= i && i < 0x11_0000);
    }

    public int Value => (int)_value;

    public bool Equals(Rune other) => this == other;

    public override bool Equals(object obj) => (obj is Rune other) && Equals(other);

    public override int GetHashCode() => Value;

    // Values are always between 0 and 0x11_0000, so overflow isn't possible
    public int CompareTo(Rune other) => this.Value - other.Value;

    int IComparable.CompareTo(object obj) {
      switch (obj) {
        case null:
          return 1; // non-null ("this") always sorts after null
        case Rune other:
          return CompareTo(other);
        default:
          throw new ArgumentException();
      }
    }

    public static bool operator ==(Rune left, Rune right) => left._value == right._value;

    public static bool operator !=(Rune left, Rune right) => left._value != right._value;

    public static bool operator <(Rune left, Rune right) => left._value < right._value;

    public static bool operator <=(Rune left, Rune right) => left._value <= right._value;

    public static bool operator >(Rune left, Rune right) => left._value > right._value;

    public static bool operator >=(Rune left, Rune right) => left._value >= right._value;

    public static explicit operator Rune(int value) => new Rune(value);
    public static explicit operator Rune(BigInteger value) => new Rune((uint)value);

    // Defined this way to be consistent with System.Text.Rune,
    // but note that Dafny will use Helpers.ToString(rune),
    // which will print in the style of a character literal instead.
    public override string ToString() {
      return char.ConvertFromUtf32(Value);
    }

    // Replacement for String.EnumerateRunes() from newer platforms
    public static IEnumerable<Rune> Enumerate(string s) {
      var sLength = s.Length;
      for (var i = 0; i < sLength; i++) {
        if (char.IsHighSurrogate(s[i])) {
          if (char.IsLowSurrogate(s[i + 1])) {
            yield return (Rune)char.ConvertToUtf32(s[i], s[i + 1]);
            i++;
          } else {
            throw new ArgumentException();
          }
        } else if (char.IsLowSurrogate(s[i])) {
          throw new ArgumentException();
        } else {
          yield return (Rune)s[i];
        }
      }
    }
  }

  public interface ISet<out T> {
    int Count { get; }
    long LongCount { get; }
    IEnumerable<T> Elements { get; }
    IEnumerable<ISet<T>> AllSubsets { get; }
    bool Contains<G>(G t);
    bool EqualsAux(ISet<object> other);
    ISet<U> DowncastClone<U>(Func<T, U> converter);
  }

  public class Set<T> : ISet<T> {
    readonly ImmutableHashSet<T> setImpl;
    readonly bool containsNull;
    Set(ImmutableHashSet<T> d, bool containsNull) {
      this.setImpl = d;
      this.containsNull = containsNull;
    }

    public static readonly ISet<T> Empty = new Set<T>(ImmutableHashSet<T>.Empty, false);

    private static readonly TypeDescriptor<ISet<T>> _TYPE = new Dafny.TypeDescriptor<ISet<T>>(Empty);
    public static TypeDescriptor<ISet<T>> _TypeDescriptor() {
      return _TYPE;
    }

    public static ISet<T> FromElements(params T[] values) {
      return FromCollection(values);
    }

    public static Set<T> FromISet(ISet<T> s) {
      return s as Set<T> ?? FromCollection(s.Elements);
    }

    public static Set<T> FromCollection(IEnumerable<T> values) {
      var d = ImmutableHashSet<T>.Empty.ToBuilder();
      var containsNull = false;
      foreach (T t in values) {
        if (t == null) {
          containsNull = true;
        } else {
          d.Add(t);
        }
      }

      return new Set<T>(d.ToImmutable(), containsNull);
    }

    public static ISet<T> FromCollectionPlusOne(IEnumerable<T> values, T oneMoreValue) {
      var d = ImmutableHashSet<T>.Empty.ToBuilder();
      var containsNull = false;
      if (oneMoreValue == null) {
        containsNull = true;
      } else {
        d.Add(oneMoreValue);
      }

      foreach (T t in values) {
        if (t == null) {
          containsNull = true;
        } else {
          d.Add(t);
        }
      }

      return new Set<T>(d.ToImmutable(), containsNull);
    }

    public ISet<U> DowncastClone<U>(Func<T, U> converter) {
      if (this is ISet<U> th) {
        return th;
      } else {
        var d = ImmutableHashSet<U>.Empty.ToBuilder();
        foreach (var t in this.setImpl) {
          var u = converter(t);
          d.Add(u);
        }

        return new Set<U>(d.ToImmutable(), this.containsNull);
      }
    }

    public int Count {
      get { return this.setImpl.Count + (containsNull ? 1 : 0); }
    }

    public long LongCount {
      get { return this.setImpl.Count + (containsNull ? 1 : 0); }
    }

    public IEnumerable<T> Elements {
      get {
        if (containsNull) {
          yield return default(T);
        }

        foreach (var t in this.setImpl) {
          yield return t;
        }
      }
    }

    /// <summary>
    /// This is an inefficient iterator for producing all subsets of "this".
    /// </summary>
    public IEnumerable<ISet<T>> AllSubsets {
      get {
        // Start by putting all set elements into a list, but don't include null
        var elmts = new List<T>();
        elmts.AddRange(this.setImpl);
        var n = elmts.Count;
        var which = new bool[n];
        var s = ImmutableHashSet<T>.Empty.ToBuilder();
        while (true) {
          // yield both the subset without null and, if null is in the original set, the subset with null included
          var ihs = s.ToImmutable();
          yield return new Set<T>(ihs, false);
          if (containsNull) {
            yield return new Set<T>(ihs, true);
          }

          // "add 1" to "which", as if doing a carry chain.  For every digit changed, change the membership of the corresponding element in "s".
          int i = 0;
          for (; i < n && which[i]; i++) {
            which[i] = false;
            s.Remove(elmts[i]);
          }

          if (i == n) {
            // we have cycled through all the subsets
            break;
          }

          which[i] = true;
          s.Add(elmts[i]);
        }
      }
    }

    public bool Equals(ISet<T> other) {
      if (ReferenceEquals(this, other)) {
        return true;
      }

      if (other == null || Count != other.Count) {
        return false;
      }

      foreach (var elmt in Elements) {
        if (!other.Contains(elmt)) {
          return false;
        }
      }

      return true;
    }

    public override bool Equals(object other) {
      if (other is ISet<T>) {
        return Equals((ISet<T>)other);
      }

      var th = this as ISet<object>;
      var oth = other as ISet<object>;
      if (th != null && oth != null) {
        // We'd like to obtain the more specific type parameter U for oth's type ISet<U>.
        // We do that by making a dynamically dispatched call, like:
        //     oth.Equals(this)
        // The hope is then that its comparison "this is ISet<U>" (that is, the first "if" test
        // above, but in the call "oth.Equals(this)") will be true and the non-virtual Equals
        // can be called. However, such a recursive call to "oth.Equals(this)" could turn
        // into infinite recursion. Therefore, we instead call "oth.EqualsAux(this)", which
        // performs the desired type test, but doesn't recurse any further.
        return oth.EqualsAux(th);
      } else {
        return false;
      }
    }

    public bool EqualsAux(ISet<object> other) {
      var s = other as ISet<T>;
      if (s != null) {
        return Equals(s);
      } else {
        return false;
      }
    }

    public override int GetHashCode() {
      var hashCode = 1;
      if (containsNull) {
        hashCode = hashCode * (Dafny.Helpers.GetHashCode(default(T)) + 3);
      }

      foreach (var t in this.setImpl) {
        hashCode = hashCode * (Dafny.Helpers.GetHashCode(t) + 3);
      }

      return hashCode;
    }

    public override string ToString() {
      var s = "{";
      var sep = "";
      if (containsNull) {
        s += sep + Dafny.Helpers.ToString(default(T));
        sep = ", ";
      }

      foreach (var t in this.setImpl) {
        s += sep + Dafny.Helpers.ToString(t);
        sep = ", ";
      }

      return s + "}";
    }
    public static bool IsProperSubsetOf(ISet<T> th, ISet<T> other) {
      return th.Count < other.Count && IsSubsetOf(th, other);
    }
    public static bool IsSubsetOf(ISet<T> th, ISet<T> other) {
      if (other.Count < th.Count) {
        return false;
      }
      foreach (T t in th.Elements) {
        if (!other.Contains(t)) {
          return false;
        }
      }
      return true;
    }
    public static bool IsDisjointFrom(ISet<T> th, ISet<T> other) {
      ISet<T> a, b;
      if (th.Count < other.Count) {
        a = th; b = other;
      } else {
        a = other; b = th;
      }
      foreach (T t in a.Elements) {
        if (b.Contains(t)) {
          return false;
        }
      }
      return true;
    }
    public bool Contains<G>(G t) {
      return t == null ? containsNull : t is T && this.setImpl.Contains((T)(object)t);
    }
    public static ISet<T> Union(ISet<T> th, ISet<T> other) {
      var a = FromISet(th);
      var b = FromISet(other);
      return new Set<T>(a.setImpl.Union(b.setImpl), a.containsNull || b.containsNull);
    }
    public static ISet<T> Intersect(ISet<T> th, ISet<T> other) {
      var a = FromISet(th);
      var b = FromISet(other);
      return new Set<T>(a.setImpl.Intersect(b.setImpl), a.containsNull && b.containsNull);
    }
    public static ISet<T> Difference(ISet<T> th, ISet<T> other) {
      var a = FromISet(th);
      var b = FromISet(other);
      return new Set<T>(a.setImpl.Except(b.setImpl), a.containsNull && !b.containsNull);
    }
  }

  public interface IMultiSet<out T> {
    bool IsEmpty { get; }
    int Count { get; }
    long LongCount { get; }
    BigInteger ElementCount { get; }
    IEnumerable<T> Elements { get; }
    IEnumerable<T> UniqueElements { get; }
    bool Contains<G>(G t);
    BigInteger Select<G>(G t);
    IMultiSet<T> Update<G>(G t, BigInteger i);
    bool EqualsAux(IMultiSet<object> other);
    IMultiSet<U> DowncastClone<U>(Func<T, U> converter);
  }

  public class MultiSet<T> : IMultiSet<T> {
    readonly ImmutableDictionary<T, BigInteger> dict;
    readonly BigInteger occurrencesOfNull;  // stupidly, a Dictionary in .NET cannot use "null" as a key
    MultiSet(ImmutableDictionary<T, BigInteger>.Builder d, BigInteger occurrencesOfNull) {
      dict = d.ToImmutable();
      this.occurrencesOfNull = occurrencesOfNull;
    }
    public static readonly MultiSet<T> Empty = new MultiSet<T>(ImmutableDictionary<T, BigInteger>.Empty.ToBuilder(), BigInteger.Zero);

    private static readonly TypeDescriptor<IMultiSet<T>> _TYPE = new Dafny.TypeDescriptor<IMultiSet<T>>(Empty);
    public static TypeDescriptor<IMultiSet<T>> _TypeDescriptor() {
      return _TYPE;
    }

    public static MultiSet<T> FromIMultiSet(IMultiSet<T> s) {
      return s as MultiSet<T> ?? FromCollection(s.Elements);
    }
    public static MultiSet<T> FromElements(params T[] values) {
      var d = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      var occurrencesOfNull = BigInteger.Zero;
      foreach (T t in values) {
        if (t == null) {
          occurrencesOfNull++;
        } else {
          if (!d.TryGetValue(t, out var i)) {
            i = BigInteger.Zero;
          }
          d[t] = i + 1;
        }
      }
      return new MultiSet<T>(d, occurrencesOfNull);
    }

    public static MultiSet<T> FromCollection(IEnumerable<T> values) {
      var d = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      var occurrencesOfNull = BigInteger.Zero;
      foreach (T t in values) {
        if (t == null) {
          occurrencesOfNull++;
        } else {
          if (!d.TryGetValue(t,
                out var i)) {
            i = BigInteger.Zero;
          }

          d[t] = i + 1;
        }
      }

      return new MultiSet<T>(d,
        occurrencesOfNull);
    }

    public static MultiSet<T> FromSeq(ISequence<T> values) {
      var d = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      var occurrencesOfNull = BigInteger.Zero;
      foreach (var t in values) {
        if (t == null) {
          occurrencesOfNull++;
        } else {
          if (!d.TryGetValue(t,
                out var i)) {
            i = BigInteger.Zero;
          }

          d[t] = i + 1;
        }
      }

      return new MultiSet<T>(d,
        occurrencesOfNull);
    }
    public static MultiSet<T> FromSet(ISet<T> values) {
      var d = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      var containsNull = false;
      foreach (T t in values.Elements) {
        if (t == null) {
          containsNull = true;
        } else {
          d[t] = BigInteger.One;
        }
      }
      return new MultiSet<T>(d, containsNull ? BigInteger.One : BigInteger.Zero);
    }
    public IMultiSet<U> DowncastClone<U>(Func<T, U> converter) {
      if (this is IMultiSet<U> th) {
        return th;
      } else {
        var d = ImmutableDictionary<U, BigInteger>.Empty.ToBuilder();
        foreach (var item in this.dict) {
          var k = converter(item.Key);
          d.Add(k, item.Value);
        }
        return new MultiSet<U>(d, this.occurrencesOfNull);
      }
    }

    public bool Equals(IMultiSet<T> other) {
      return IsSubsetOf(this, other) && IsSubsetOf(other, this);
    }
    public override bool Equals(object other) {
      if (other is IMultiSet<T>) {
        return Equals((IMultiSet<T>)other);
      }
      var th = this as IMultiSet<object>;
      var oth = other as IMultiSet<object>;
      if (th != null && oth != null) {
        // See comment in Set.Equals
        return oth.EqualsAux(th);
      } else {
        return false;
      }
    }

    public bool EqualsAux(IMultiSet<object> other) {
      var s = other as IMultiSet<T>;
      if (s != null) {
        return Equals(s);
      } else {
        return false;
      }
    }

    public override int GetHashCode() {
      var hashCode = 1;
      if (occurrencesOfNull > 0) {
        var key = Dafny.Helpers.GetHashCode(default(T));
        key = (key << 3) | (key >> 29) ^ occurrencesOfNull.GetHashCode();
        hashCode = hashCode * (key + 3);
      }
      foreach (var kv in dict) {
        var key = Dafny.Helpers.GetHashCode(kv.Key);
        key = (key << 3) | (key >> 29) ^ kv.Value.GetHashCode();
        hashCode = hashCode * (key + 3);
      }
      return hashCode;
    }
    public override string ToString() {
      var s = "multiset{";
      var sep = "";
      for (var i = BigInteger.Zero; i < occurrencesOfNull; i++) {
        s += sep + Dafny.Helpers.ToString(default(T));
        sep = ", ";
      }
      foreach (var kv in dict) {
        var t = Dafny.Helpers.ToString(kv.Key);
        for (var i = BigInteger.Zero; i < kv.Value; i++) {
          s += sep + t;
          sep = ", ";
        }
      }
      return s + "}";
    }
    public static bool IsProperSubsetOf(IMultiSet<T> th, IMultiSet<T> other) {
      // Be sure to use ElementCount to avoid casting into 32 bits
      // integers that could lead to overflows (see https://github.com/dafny-lang/dafny/issues/5554)
      return th.ElementCount < other.ElementCount && IsSubsetOf(th, other);
    }
    public static bool IsSubsetOf(IMultiSet<T> th, IMultiSet<T> other) {
      var a = FromIMultiSet(th);
      var b = FromIMultiSet(other);
      if (b.occurrencesOfNull < a.occurrencesOfNull) {
        return false;
      }
      foreach (T t in a.dict.Keys) {
        if (b.dict.ContainsKey(t)) {
          if (b.dict[t] < a.dict[t]) {
            return false;
          }
        } else {
          if (a.dict[t] != BigInteger.Zero) {
            return false;
          }
        }
      }
      return true;
    }
    public static bool IsDisjointFrom(IMultiSet<T> th, IMultiSet<T> other) {
      foreach (T t in th.UniqueElements) {
        if (other.Contains(t)) {
          return false;
        }
      }
      return true;
    }

    public bool Contains<G>(G t) {
      return Select(t) != 0;
    }
    public BigInteger Select<G>(G t) {
      if (t == null) {
        return occurrencesOfNull;
      }

      if (t is T && dict.TryGetValue((T)(object)t, out var m)) {
        return m;
      } else {
        return BigInteger.Zero;
      }
    }
    public IMultiSet<T> Update<G>(G t, BigInteger i) {
      if (Select(t) == i) {
        return this;
      } else if (t == null) {
        var r = dict.ToBuilder();
        return new MultiSet<T>(r, i);
      } else {
        var r = dict.ToBuilder();
        r[(T)(object)t] = i;
        return new MultiSet<T>(r, occurrencesOfNull);
      }
    }
    public static IMultiSet<T> Union(IMultiSet<T> th, IMultiSet<T> other) {
      if (th.IsEmpty) {
        return other;
      } else if (other.IsEmpty) {
        return th;
      }
      var a = FromIMultiSet(th);
      var b = FromIMultiSet(other);
      var r = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      foreach (T t in a.dict.Keys) {
        if (!r.TryGetValue(t, out var i)) {
          i = BigInteger.Zero;
        }
        r[t] = i + a.dict[t];
      }
      foreach (T t in b.dict.Keys) {
        if (!r.TryGetValue(t, out var i)) {
          i = BigInteger.Zero;
        }
        r[t] = i + b.dict[t];
      }
      return new MultiSet<T>(r, a.occurrencesOfNull + b.occurrencesOfNull);
    }
    public static IMultiSet<T> Intersect(IMultiSet<T> th, IMultiSet<T> other) {
      if (th.IsEmpty) {
        return th;
      } else if (other.IsEmpty) {
        return other;
      }
      var a = FromIMultiSet(th);
      var b = FromIMultiSet(other);
      var r = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      foreach (T t in a.dict.Keys) {
        if (b.dict.ContainsKey(t)) {
          r.Add(t, a.dict[t] < b.dict[t] ? a.dict[t] : b.dict[t]);
        }
      }
      return new MultiSet<T>(r, a.occurrencesOfNull < b.occurrencesOfNull ? a.occurrencesOfNull : b.occurrencesOfNull);
    }
    public static IMultiSet<T> Difference(IMultiSet<T> th, IMultiSet<T> other) { // \result == this - other
      if (other.IsEmpty) {
        return th;
      }
      var a = FromIMultiSet(th);
      var b = FromIMultiSet(other);
      var r = ImmutableDictionary<T, BigInteger>.Empty.ToBuilder();
      foreach (T t in a.dict.Keys) {
        if (!b.dict.ContainsKey(t)) {
          r.Add(t, a.dict[t]);
        } else if (b.dict[t] < a.dict[t]) {
          r.Add(t, a.dict[t] - b.dict[t]);
        }
      }
      return new MultiSet<T>(r, b.occurrencesOfNull < a.occurrencesOfNull ? a.occurrencesOfNull - b.occurrencesOfNull : BigInteger.Zero);
    }

    public bool IsEmpty { get { return occurrencesOfNull == 0 && dict.IsEmpty; } }

    public int Count {
      get { return (int)ElementCount; }
    }
    public long LongCount {
      get { return (long)ElementCount; }
    }

    public BigInteger ElementCount {
      get {
        // This is inefficient
        var c = occurrencesOfNull;
        foreach (var item in dict) {
          c += item.Value;
        }
        return c;
      }
    }

    public IEnumerable<T> Elements {
      get {
        for (var i = BigInteger.Zero; i < occurrencesOfNull; i++) {
          yield return default(T);
        }
        foreach (var item in dict) {
          for (var i = BigInteger.Zero; i < item.Value; i++) {
            yield return item.Key;
          }
        }
      }
    }

    public IEnumerable<T> UniqueElements {
      get {
        if (!occurrencesOfNull.IsZero) {
          yield return default(T);
        }
        foreach (var key in dict.Keys) {
          if (dict[key] != 0) {
            yield return key;
          }
        }
      }
    }
  }

  public interface IMap<out U, out V> {
    int Count { get; }
    long LongCount { get; }
    ISet<U> Keys { get; }
    ISet<V> Values { get; }
    IEnumerable<IPair<U, V>> ItemEnumerable { get; }
    bool Contains<G>(G t);
    /// <summary>
    /// Returns "true" iff "this is IMap<object, object>" and "this" equals "other".
    /// </summary>
    bool EqualsObjObj(IMap<object, object> other);
    IMap<UU, VV> DowncastClone<UU, VV>(Func<U, UU> keyConverter, Func<V, VV> valueConverter);
  }

  public class Map<U, V> : IMap<U, V> {
    readonly ImmutableDictionary<U, V> dict;
    readonly bool hasNullKey;  // true when "null" is a key of the Map
    readonly V nullValue;  // if "hasNullKey", the value that "null" maps to

    private Map(ImmutableDictionary<U, V>.Builder d, bool hasNullKey, V nullValue) {
      dict = d.ToImmutable();
      this.hasNullKey = hasNullKey;
      this.nullValue = nullValue;
    }
    public static readonly Map<U, V> Empty = new Map<U, V>(ImmutableDictionary<U, V>.Empty.ToBuilder(), false, default(V));

    private Map(ImmutableDictionary<U, V> d, bool hasNullKey, V nullValue) {
      dict = d;
      this.hasNullKey = hasNullKey;
      this.nullValue = nullValue;
    }

    private static readonly TypeDescriptor<IMap<U, V>> _TYPE = new Dafny.TypeDescriptor<IMap<U, V>>(Empty);
    public static TypeDescriptor<IMap<U, V>> _TypeDescriptor() {
      return _TYPE;
    }

    public static Map<U, V> FromElements(params IPair<U, V>[] values) {
      var d = ImmutableDictionary<U, V>.Empty.ToBuilder();
      var hasNullKey = false;
      var nullValue = default(V);
      foreach (var p in values) {
        if (p.Car == null) {
          hasNullKey = true;
          nullValue = p.Cdr;
        } else {
          d[p.Car] = p.Cdr;
        }
      }
      return new Map<U, V>(d, hasNullKey, nullValue);
    }
    public static Map<U, V> FromCollection(IEnumerable<IPair<U, V>> values) {
      var d = ImmutableDictionary<U, V>.Empty.ToBuilder();
      var hasNullKey = false;
      var nullValue = default(V);
      foreach (var p in values) {
        if (p.Car == null) {
          hasNullKey = true;
          nullValue = p.Cdr;
        } else {
          d[p.Car] = p.Cdr;
        }
      }
      return new Map<U, V>(d, hasNullKey, nullValue);
    }
    public static Map<U, V> FromIMap(IMap<U, V> m) {
      return m as Map<U, V> ?? FromCollection(m.ItemEnumerable);
    }
    public IMap<UU, VV> DowncastClone<UU, VV>(Func<U, UU> keyConverter, Func<V, VV> valueConverter) {
      if (this is IMap<UU, VV> th) {
        return th;
      } else {
        var d = ImmutableDictionary<UU, VV>.Empty.ToBuilder();
        foreach (var item in this.dict) {
          var k = keyConverter(item.Key);
          var v = valueConverter(item.Value);
          d.Add(k, v);
        }
        return new Map<UU, VV>(d, this.hasNullKey, (VV)(object)this.nullValue);
      }
    }
    public int Count {
      get { return dict.Count + (hasNullKey ? 1 : 0); }
    }
    public long LongCount {
      get { return dict.Count + (hasNullKey ? 1 : 0); }
    }

    public bool Equals(IMap<U, V> other) {
      if (ReferenceEquals(this, other)) {
        return true;
      }

      if (other == null || LongCount != other.LongCount) {
        return false;
      }

      if (hasNullKey) {
        if (!other.Contains(default(U)) || !object.Equals(nullValue, Select(other, default(U)))) {
          return false;
        }
      }

      foreach (var item in dict) {
        if (!other.Contains(item.Key) || !object.Equals(item.Value, Select(other, item.Key))) {
          return false;
        }
      }
      return true;
    }
    public bool EqualsObjObj(IMap<object, object> other) {
      if (ReferenceEquals(this, other)) {
        return true;
      }
      if (!(this is IMap<object, object>) || other == null || LongCount != other.LongCount) {
        return false;
      }
      var oth = Map<object, object>.FromIMap(other);
      if (hasNullKey) {
        if (!oth.Contains(default(U)) || !object.Equals(nullValue, Map<object, object>.Select(oth, default(U)))) {
          return false;
        }
      }
      foreach (var item in dict) {
        if (!other.Contains(item.Key) || !object.Equals(item.Value, Map<object, object>.Select(oth, item.Key))) {
          return false;
        }
      }
      return true;
    }
    public override bool Equals(object other) {
      // See comment in Set.Equals
      var m = other as IMap<U, V>;
      if (m != null) {
        return Equals(m);
      }
      var imapoo = other as IMap<object, object>;
      if (imapoo != null) {
        return EqualsObjObj(imapoo);
      } else {
        return false;
      }
    }

    public override int GetHashCode() {
      var hashCode = 1;
      if (hasNullKey) {
        var key = Dafny.Helpers.GetHashCode(default(U));
        key = (key << 3) | (key >> 29) ^ Dafny.Helpers.GetHashCode(nullValue);
        hashCode = hashCode * (key + 3);
      }
      foreach (var kv in dict) {
        var key = Dafny.Helpers.GetHashCode(kv.Key);
        key = (key << 3) | (key >> 29) ^ Dafny.Helpers.GetHashCode(kv.Value);
        hashCode = hashCode * (key + 3);
      }
      return hashCode;
    }
    public override string ToString() {
      var s = "map[";
      var sep = "";
      if (hasNullKey) {
        s += sep + Dafny.Helpers.ToString(default(U)) + " := " + Dafny.Helpers.ToString(nullValue);
        sep = ", ";
      }
      foreach (var kv in dict) {
        s += sep + Dafny.Helpers.ToString(kv.Key) + " := " + Dafny.Helpers.ToString(kv.Value);
        sep = ", ";
      }
      return s + "]";
    }
    public bool Contains<G>(G u) {
      return u == null ? hasNullKey : u is U && dict.ContainsKey((U)(object)u);
    }
    public static V Select(IMap<U, V> th, U index) {
      // the following will throw an exception if "index" in not a key of the map
      var m = FromIMap(th);
      return index == null && m.hasNullKey ? m.nullValue : m.dict[index];
    }
    public static IMap<U, V> Update(IMap<U, V> th, U index, V val) {
      var m = FromIMap(th);
      var d = m.dict.ToBuilder();
      if (index == null) {
        return new Map<U, V>(d, true, val);
      } else {
        d[index] = val;
        return new Map<U, V>(d, m.hasNullKey, m.nullValue);
      }
    }

    public static IMap<U, V> Merge(IMap<U, V> th, IMap<U, V> other) {
      var a = FromIMap(th);
      var b = FromIMap(other);
      ImmutableDictionary<U, V> d = a.dict.SetItems(b.dict);
      return new Map<U, V>(d, a.hasNullKey || b.hasNullKey, b.hasNullKey ? b.nullValue : a.nullValue);
    }

    public static IMap<U, V> Subtract(IMap<U, V> th, ISet<U> keys) {
      var a = FromIMap(th);
      ImmutableDictionary<U, V> d = a.dict.RemoveRange(keys.Elements);
      return new Map<U, V>(d, a.hasNullKey && !keys.Contains<object>(null), a.nullValue);
    }

    public ISet<U> Keys {
      get {
        if (hasNullKey) {
          return Dafny.Set<U>.FromCollectionPlusOne(dict.Keys, default(U));
        } else {
          return Dafny.Set<U>.FromCollection(dict.Keys);
        }
      }
    }
    public ISet<V> Values {
      get {
        if (hasNullKey) {
          return Dafny.Set<V>.FromCollectionPlusOne(dict.Values, nullValue);
        } else {
          return Dafny.Set<V>.FromCollection(dict.Values);
        }
      }
    }

    public IEnumerable<IPair<U, V>> ItemEnumerable {
      get {
        if (hasNullKey) {
          yield return new Pair<U, V>(default(U), nullValue);
        }
        foreach (KeyValuePair<U, V> kvp in dict) {
          yield return new Pair<U, V>(kvp.Key, kvp.Value);
        }
      }
    }

    public static ISet<_System._ITuple2<U, V>> Items(IMap<U, V> m) {
      var result = new HashSet<_System._ITuple2<U, V>>();
      foreach (var item in m.ItemEnumerable) {
        result.Add(_System.Tuple2<U, V>.create(item.Car, item.Cdr));
      }
      return Dafny.Set<_System._ITuple2<U, V>>.FromCollection(result);
    }
  }

  public interface ISequence<out T> : IEnumerable<T> {
    long LongCount { get; }
    int Count { get; }
    [Obsolete("Use CloneAsArray() instead of Elements (both perform a copy).")]
    T[] Elements { get; }
    T[] CloneAsArray();
    IEnumerable<T> UniqueElements { get; }
    T Select(ulong index);
    T Select(long index);
    T Select(uint index);
    T Select(int index);
    T Select(BigInteger index);
    bool Contains<G>(G g);
    ISequence<T> Take(long m);
    ISequence<T> Take(ulong n);
    ISequence<T> Take(BigInteger n);
    ISequence<T> Drop(long m);
    ISequence<T> Drop(ulong n);
    ISequence<T> Drop(BigInteger n);
    ISequence<T> Subsequence(long lo, long hi);
    ISequence<T> Subsequence(long lo, ulong hi);
    ISequence<T> Subsequence(long lo, BigInteger hi);
    ISequence<T> Subsequence(ulong lo, long hi);
    ISequence<T> Subsequence(ulong lo, ulong hi);
    ISequence<T> Subsequence(ulong lo, BigInteger hi);
    ISequence<T> Subsequence(BigInteger lo, long hi);
    ISequence<T> Subsequence(BigInteger lo, ulong hi);
    ISequence<T> Subsequence(BigInteger lo, BigInteger hi);
    bool EqualsAux(ISequence<object> other);
    ISequence<U> DowncastClone<U>(Func<T, U> converter);
    string ToVerbatimString(bool asLiteral);
  }

  public abstract class Sequence<T> : ISequence<T> {
    public static readonly ISequence<T> Empty = new ArraySequence<T>(new T[0]);

    private static readonly TypeDescriptor<ISequence<T>> _TYPE = new Dafny.TypeDescriptor<ISequence<T>>(Empty);
    public static TypeDescriptor<ISequence<T>> _TypeDescriptor() {
      return _TYPE;
    }

    public static ISequence<T> Create(BigInteger length, System.Func<BigInteger, T> init) {
      var len = (int)length;
      var builder = ImmutableArray.CreateBuilder<T>(len);
      for (int i = 0; i < len; i++) {
        builder.Add(init(new BigInteger(i)));
      }
      return new ArraySequence<T>(builder.MoveToImmutable());
    }
    public static ISequence<T> FromArray(T[] values) {
      return new ArraySequence<T>(values);
    }
    public static ISequence<T> FromElements(params T[] values) {
      return new ArraySequence<T>(values);
    }
    public static ISequence<char> FromString(string s) {
      return new ArraySequence<char>(s.ToCharArray());
    }
    public static ISequence<Rune> UnicodeFromString(string s) {
      var runes = new List<Rune>();

      foreach (var rune in Rune.Enumerate(s)) {
        runes.Add(rune);
      }
      return new ArraySequence<Rune>(runes.ToArray());
    }

    public static ISequence<ISequence<char>> FromMainArguments(string[] args) {
      Dafny.ISequence<char>[] dafnyArgs = new Dafny.ISequence<char>[args.Length + 1];
      dafnyArgs[0] = Dafny.Sequence<char>.FromString("dotnet");
      for (var i = 0; i < args.Length; i++) {
        dafnyArgs[i + 1] = Dafny.Sequence<char>.FromString(args[i]);
      }

      return Sequence<ISequence<char>>.FromArray(dafnyArgs);
    }
    public static ISequence<ISequence<Rune>> UnicodeFromMainArguments(string[] args) {
      Dafny.ISequence<Rune>[] dafnyArgs = new Dafny.ISequence<Rune>[args.Length + 1];
      dafnyArgs[0] = Dafny.Sequence<Rune>.UnicodeFromString("dotnet");
      for (var i = 0; i < args.Length; i++) {
        dafnyArgs[i + 1] = Dafny.Sequence<Rune>.UnicodeFromString(args[i]);
      }

      return Sequence<ISequence<Rune>>.FromArray(dafnyArgs);
    }

    public ISequence<U> DowncastClone<U>(Func<T, U> converter) {
      if (this is ISequence<U> th) {
        return th;
      } else {
        var values = new U[this.LongCount];
        for (long i = 0; i < this.LongCount; i++) {
          var val = converter(this.Select(i));
          values[i] = val;
        }
        return new ArraySequence<U>(values);
      }
    }
    public static ISequence<T> Update(ISequence<T> sequence, long index, T t) {
      T[] tmp = sequence.CloneAsArray();
      tmp[index] = t;
      return new ArraySequence<T>(tmp);
    }
    public static ISequence<T> Update(ISequence<T> sequence, ulong index, T t) {
      return Update(sequence, (long)index, t);
    }
    public static ISequence<T> Update(ISequence<T> sequence, BigInteger index, T t) {
      return Update(sequence, (long)index, t);
    }
    public static bool EqualUntil(ISequence<T> left, ISequence<T> right, int n) {
      for (int i = 0; i < n; i++) {
        if (!Equals(left.Select(i), right.Select(i))) {
          return false;
        }
      }
      return true;
    }
    public static bool IsPrefixOf(ISequence<T> left, ISequence<T> right) {
      int n = left.Count;
      return n <= right.Count && EqualUntil(left, right, n);
    }
    public static bool IsProperPrefixOf(ISequence<T> left, ISequence<T> right) {
      int n = left.Count;
      return n < right.Count && EqualUntil(left, right, n);
    }
    public static ISequence<T> Concat(ISequence<T> left, ISequence<T> right) {
      if (left.Count == 0) {
        return right;
      }
      if (right.Count == 0) {
        return left;
      }
      return new ConcatSequence<T>(left, right);
    }
    // Make Count a public abstract instead of LongCount, since the "array size is limited to a total of 4 billion
    // elements, and to a maximum index of 0X7FEFFFFF". Therefore, as a protection, limit this to int32.
    // https://docs.microsoft.com/en-us/dotnet/api/system.array
    public abstract int Count { get; }
    public long LongCount {
      get { return Count; }
    }
    // ImmutableElements cannot be public in the interface since ImmutableArray<T> leads to a
    // "covariant type T occurs in invariant position" error. There do not appear to be interfaces for ImmutableArray<T>
    // that resolve this.
    internal abstract ImmutableArray<T> ImmutableElements { get; }

    public T[] Elements { get { return CloneAsArray(); } }

    public T[] CloneAsArray() {
      return ImmutableElements.ToArray();
    }

    public IEnumerable<T> UniqueElements {
      get {
        return Set<T>.FromCollection(ImmutableElements).Elements;
      }
    }

    public IEnumerator<T> GetEnumerator() {
      foreach (var el in ImmutableElements) {
        yield return el;
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public T Select(ulong index) {
      return ImmutableElements[checked((int)index)];
    }
    public T Select(long index) {
      return ImmutableElements[checked((int)index)];
    }
    public T Select(uint index) {
      return ImmutableElements[checked((int)index)];
    }
    public T Select(int index) {
      return ImmutableElements[index];
    }
    public T Select(BigInteger index) {
      return ImmutableElements[(int)index];
    }
    public bool Equals(ISequence<T> other) {
      return ReferenceEquals(this, other) || (Count == other.Count && EqualUntil(this, other, Count));
    }
    public override bool Equals(object other) {
      if (other is ISequence<T>) {
        return Equals((ISequence<T>)other);
      }
      var th = this as ISequence<object>;
      var oth = other as ISequence<object>;
      if (th != null && oth != null) {
        // see explanation in Set.Equals
        return oth.EqualsAux(th);
      } else {
        return false;
      }
    }
    public bool EqualsAux(ISequence<object> other) {
      var s = other as ISequence<T>;
      if (s != null) {
        return Equals(s);
      } else {
        return false;
      }
    }
    public override int GetHashCode() {
      ImmutableArray<T> elmts = ImmutableElements;
      // https://devblogs.microsoft.com/dotnet/please-welcome-immutablearrayt/
      if (elmts.IsDefaultOrEmpty) {
        return 0;
      }

      var hashCode = 0;
      for (var i = 0; i < elmts.Length; i++) {
        hashCode = (hashCode << 3) | (hashCode >> 29) ^ Dafny.Helpers.GetHashCode(elmts[i]);
      }
      return hashCode;
    }
    public override string ToString() {
      if (typeof(T) == typeof(char)) {
        return string.Concat(this);
      } else {
        return "[" + string.Join(", ", ImmutableElements.Select(Dafny.Helpers.ToString)) + "]";
      }
    }

    public string ToVerbatimString(bool asLiteral) {
      var builder = new System.Text.StringBuilder();
      if (asLiteral) {
        builder.Append('"');
      }
      foreach (var c in this) {
        var rune = (Rune)(object)c;
        if (asLiteral) {
          builder.Append(Helpers.EscapeCharacter(rune));
        } else {
          builder.Append(char.ConvertFromUtf32(rune.Value));
        }
      }
      if (asLiteral) {
        builder.Append('"');
      }
      return builder.ToString();
    }

    public bool Contains<G>(G g) {
      if (g == null || g is T) {
        var t = (T)(object)g;
        return ImmutableElements.Contains(t);
      }
      return false;
    }
    public ISequence<T> Take(long m) {
      return Subsequence(0, m);
    }
    public ISequence<T> Take(ulong n) {
      return Take((long)n);
    }
    public ISequence<T> Take(BigInteger n) {
      return Take((long)n);
    }
    public ISequence<T> Drop(long m) {
      return Subsequence(m, Count);
    }
    public ISequence<T> Drop(ulong n) {
      return Drop((long)n);
    }
    public ISequence<T> Drop(BigInteger n) {
      return Drop((long)n);
    }
    public ISequence<T> Subsequence(long lo, long hi) {
      if (lo == 0 && hi == Count) {
        return this;
      }
      int startingIndex = checked((int)lo);
      var length = checked((int)hi) - startingIndex;
      return new ArraySequence<T>(ImmutableArray.Create<T>(ImmutableElements, startingIndex, length));
    }
    public ISequence<T> Subsequence(long lo, ulong hi) {
      return Subsequence(lo, (long)hi);
    }
    public ISequence<T> Subsequence(long lo, BigInteger hi) {
      return Subsequence(lo, (long)hi);
    }
    public ISequence<T> Subsequence(ulong lo, long hi) {
      return Subsequence((long)lo, hi);
    }
    public ISequence<T> Subsequence(ulong lo, ulong hi) {
      return Subsequence((long)lo, (long)hi);
    }
    public ISequence<T> Subsequence(ulong lo, BigInteger hi) {
      return Subsequence((long)lo, (long)hi);
    }
    public ISequence<T> Subsequence(BigInteger lo, long hi) {
      return Subsequence((long)lo, hi);
    }
    public ISequence<T> Subsequence(BigInteger lo, ulong hi) {
      return Subsequence((long)lo, (long)hi);
    }
    public ISequence<T> Subsequence(BigInteger lo, BigInteger hi) {
      return Subsequence((long)lo, (long)hi);
    }
  }

  internal class ArraySequence<T> : Sequence<T> {
    private readonly ImmutableArray<T> elmts;

    internal ArraySequence(ImmutableArray<T> ee) {
      elmts = ee;
    }
    internal ArraySequence(T[] ee) {
      elmts = ImmutableArray.Create<T>(ee);
    }

    internal override ImmutableArray<T> ImmutableElements {
      get {
        return elmts;
      }
    }

    public override int Count {
      get {
        return elmts.Length;
      }
    }
  }

  internal class ConcatSequence<T> : Sequence<T> {
    // INVARIANT: Either left != null, right != null, and elmts's underlying array == null or
    // left == null, right == null, and elmts's underlying array != null
    internal volatile ISequence<T> left, right;
    internal ImmutableArray<T> elmts;
    private readonly int count;

    internal ConcatSequence(ISequence<T> left, ISequence<T> right) {
      this.left = left;
      this.right = right;
      this.count = left.Count + right.Count;
    }

    internal override ImmutableArray<T> ImmutableElements {
      get {
        // IsDefault returns true if the underlying array is a null reference
        // https://devblogs.microsoft.com/dotnet/please-welcome-immutablearrayt/
        if (elmts.IsDefault) {
          elmts = ComputeElements();
          // We don't need the original sequences anymore; let them be
          // garbage-collected
          left = null;
          right = null;
        }
        return elmts;
      }
    }

    public override int Count {
      get {
        return count;
      }
    }

    internal ImmutableArray<T> ComputeElements() {
      // Traverse the tree formed by all descendants which are ConcatSequences
      var ansBuilder = ImmutableArray.CreateBuilder<T>(count);
      var toVisit = new Stack<ISequence<T>>();
      var leftBuffer = left;
      var rightBuffer = right;
      if (left == null || right == null) {
        // elmts can't be .IsDefault while either left, or right are null
        return elmts;
      }
      toVisit.Push(rightBuffer);
      toVisit.Push(leftBuffer);

      while (toVisit.Count != 0) {
        var seq = toVisit.Pop();
        if (seq is ConcatSequence<T> cs && cs.elmts.IsDefault) {
          leftBuffer = cs.left;
          rightBuffer = cs.right;
          if (cs.left == null || cs.right == null) {
            // !cs.elmts.IsDefault, due to concurrent enumeration
            toVisit.Push(cs);
          } else {
            toVisit.Push(rightBuffer);
            toVisit.Push(leftBuffer);
          }
        } else {
          if (seq is Sequence<T> sq) {
            ansBuilder.AddRange(sq.ImmutableElements); // Optimized path for ImmutableArray
          } else {
            ansBuilder.AddRange(seq); // Slower path using IEnumerable
          }
        }
      }
      return ansBuilder.MoveToImmutable();
    }
  }

  public interface IPair<out A, out B> {
    A Car { get; }
    B Cdr { get; }
  }

  public class Pair<A, B> : IPair<A, B> {
    private A car;
    private B cdr;
    public A Car { get { return car; } }
    public B Cdr { get { return cdr; } }
    public Pair(A a, B b) {
      this.car = a;
      this.cdr = b;
    }
  }

  public class TypeDescriptor<T> {
    private readonly T initValue;
    public TypeDescriptor(T initValue) {
      this.initValue = initValue;
    }
    public T Default() {
      return initValue;
    }
  }

  public partial class Helpers {
    public static int GetHashCode<G>(G g) {
      return g == null ? 1001 : g.GetHashCode();
    }

    public static int ToIntChecked(BigInteger i, string msg) {
      if (i > Int32.MaxValue || i < Int32.MinValue) {
        if (msg == null) {
          msg = "value out of range for a 32-bit int";
        }

        throw new HaltException(msg + ": " + i);
      }
      return (int)i;
    }
    public static int ToIntChecked(long i, string msg) {
      if (i > Int32.MaxValue || i < Int32.MinValue) {
        if (msg == null) {
          msg = "value out of range for a 32-bit int";
        }

        throw new HaltException(msg + ": " + i);
      }
      return (int)i;
    }
    public static int ToIntChecked(int i, string msg) {
      return i;
    }

    public static string ToString<G>(G g) {
      if (g == null) {
        return "null";
      } else if (g is bool) {
        return (bool)(object)g ? "true" : "false";  // capitalize boolean literals like in Dafny
      } else if (g is Rune) {
        return "'" + EscapeCharacter((Rune)(object)g) + "'";
      } else {
        return g.ToString();
      }
    }

    public static string EscapeCharacter(Rune r) {
      switch (r.Value) {
        case '\n': return "\\n";
        case '\r': return "\\r";
        case '\t': return "\\t";
        case '\0': return "\\0";
        case '\'': return "\\'";
        case '\"': return "\\\"";
        case '\\': return "\\\\";
        default: return r.ToString();
      };
    }

    public static void Print<G>(G g) {
      System.Console.Write(ToString(g));
    }

    public static readonly TypeDescriptor<bool> BOOL = new TypeDescriptor<bool>(false);
    public static readonly TypeDescriptor<char> CHAR = new TypeDescriptor<char>('D');  // See CharType.DefaultValue in Dafny source code
    public static readonly TypeDescriptor<Rune> RUNE = new TypeDescriptor<Rune>(new Rune('D'));  // See CharType.DefaultValue in Dafny source code
    public static readonly TypeDescriptor<BigInteger> INT = new TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static readonly TypeDescriptor<BigRational> REAL = new TypeDescriptor<BigRational>(BigRational.ZERO);
    public static readonly TypeDescriptor<byte> UINT8 = new TypeDescriptor<byte>(0);
    public static readonly TypeDescriptor<ushort> UINT16 = new TypeDescriptor<ushort>(0);
    public static readonly TypeDescriptor<uint> UINT32 = new TypeDescriptor<uint>(0);
    public static readonly TypeDescriptor<ulong> UINT64 = new TypeDescriptor<ulong>(0);

    public static TypeDescriptor<T> NULL<T>() where T : class {
      return new TypeDescriptor<T>(null);
    }

    public static TypeDescriptor<A[]> ARRAY<A>() {
      return new TypeDescriptor<A[]>(new A[0]);
    }

    public static bool Quantifier<T>(IEnumerable<T> vals, bool frall, System.Predicate<T> pred) {
      foreach (var u in vals) {
        if (pred(u) != frall) { return !frall; }
      }
      return frall;
    }
    // Enumerating other collections
    public static IEnumerable<bool> AllBooleans() {
      yield return false;
      yield return true;
    }
    public static IEnumerable<char> AllChars() {
      for (int i = 0; i < 0x1_0000; i++) {
        yield return (char)i;
      }
    }
    public static IEnumerable<Rune> AllUnicodeChars() {
      for (int i = 0; i < 0xD800; i++) {
        yield return new Rune(i);
      }
      for (int i = 0xE000; i < 0x11_0000; i++) {
        yield return new Rune(i);
      }
    }
    public static IEnumerable<BigInteger> AllIntegers() {
      yield return new BigInteger(0);
      for (var j = new BigInteger(1); ; j++) {
        yield return j;
        yield return -j;
      }
    }
    public static IEnumerable<BigInteger> IntegerRange(Nullable<BigInteger> lo, Nullable<BigInteger> hi) {
      if (lo == null) {
        for (var j = (BigInteger)hi; true;) {
          j--;
          yield return j;
        }
      } else if (hi == null) {
        for (var j = (BigInteger)lo; true; j++) {
          yield return j;
        }
      } else {
        for (var j = (BigInteger)lo; j < hi; j++) {
          yield return j;
        }
      }
    }
    public static IEnumerable<T> SingleValue<T>(T e) {
      yield return e;
    }
    // pre: b != 0
    // post: result == a/b, as defined by Euclidean Division (http://en.wikipedia.org/wiki/Modulo_operation)
    public static sbyte EuclideanDivision_sbyte(sbyte a, sbyte b) {
      return (sbyte)EuclideanDivision_int(a, b);
    }
    public static short EuclideanDivision_short(short a, short b) {
      return (short)EuclideanDivision_int(a, b);
    }
    public static int EuclideanDivision_int(int a, int b) {
      if (0 <= a) {
        if (0 <= b) {
          // +a +b: a/b
          return (int)(((uint)(a)) / ((uint)(b)));
        } else {
          // +a -b: -(a/(-b))
          return -((int)(((uint)(a)) / ((uint)(unchecked(-b)))));
        }
      } else {
        if (0 <= b) {
          // -a +b: -((-a-1)/b) - 1
          return -((int)(((uint)(-(a + 1))) / ((uint)(b)))) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return ((int)(((uint)(-(a + 1))) / ((uint)(unchecked(-b))))) + 1;
        }
      }
    }
    public static long EuclideanDivision_long(long a, long b) {
      if (0 <= a) {
        if (0 <= b) {
          // +a +b: a/b
          return (long)(((ulong)(a)) / ((ulong)(b)));
        } else {
          // +a -b: -(a/(-b))
          return -((long)(((ulong)(a)) / ((ulong)(unchecked(-b)))));
        }
      } else {
        if (0 <= b) {
          // -a +b: -((-a-1)/b) - 1
          return -((long)(((ulong)(-(a + 1))) / ((ulong)(b)))) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return ((long)(((ulong)(-(a + 1))) / ((ulong)(unchecked(-b))))) + 1;
        }
      }
    }
    public static BigInteger EuclideanDivision(BigInteger a, BigInteger b) {
      if (0 <= a.Sign) {
        if (0 <= b.Sign) {
          // +a +b: a/b
          return BigInteger.Divide(a, b);
        } else {
          // +a -b: -(a/(-b))
          return BigInteger.Negate(BigInteger.Divide(a, BigInteger.Negate(b)));
        }
      } else {
        if (0 <= b.Sign) {
          // -a +b: -((-a-1)/b) - 1
          return BigInteger.Negate(BigInteger.Divide(BigInteger.Negate(a) - 1, b)) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return BigInteger.Divide(BigInteger.Negate(a) - 1, BigInteger.Negate(b)) + 1;
        }
      }
    }
    // pre: b != 0
    // post: result == a%b, as defined by Euclidean Division (http://en.wikipedia.org/wiki/Modulo_operation)
    public static sbyte EuclideanModulus_sbyte(sbyte a, sbyte b) {
      return (sbyte)EuclideanModulus_int(a, b);
    }
    public static short EuclideanModulus_short(short a, short b) {
      return (short)EuclideanModulus_int(a, b);
    }
    public static int EuclideanModulus_int(int a, int b) {
      uint bp = (0 <= b) ? (uint)b : (uint)(unchecked(-b));
      if (0 <= a) {
        // +a: a % b'
        return (int)(((uint)a) % bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        uint c = ((uint)(unchecked(-a))) % bp;
        return (int)(c == 0 ? c : bp - c);
      }
    }
    public static long EuclideanModulus_long(long a, long b) {
      ulong bp = (0 <= b) ? (ulong)b : (ulong)(unchecked(-b));
      if (0 <= a) {
        // +a: a % b'
        return (long)(((ulong)a) % bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        ulong c = ((ulong)(unchecked(-a))) % bp;
        return (long)(c == 0 ? c : bp - c);
      }
    }
    public static BigInteger EuclideanModulus(BigInteger a, BigInteger b) {
      var bp = BigInteger.Abs(b);
      if (0 <= a.Sign) {
        // +a: a % b'
        return BigInteger.Remainder(a, bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        var c = BigInteger.Remainder(BigInteger.Negate(a), bp);
        return c.IsZero ? c : BigInteger.Subtract(bp, c);
      }
    }

    public static U CastConverter<T, U>(T t) {
      return (U)(object)t;
    }

    public static Sequence<T> SeqFromArray<T>(T[] array) {
      return new ArraySequence<T>(array);
    }
    // In .NET version 4.5, it is possible to mark a method with "AggressiveInlining", which says to inline the
    // method if possible.  Method "ExpressionSequence" would be a good candidate for it:
    // [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static U ExpressionSequence<T, U>(T t, U u) {
      return u;
    }

    public static U Let<T, U>(T t, Func<T, U> f) {
      return f(t);
    }

    public static A Id<A>(A a) {
      return a;
    }

    public static void WithHaltHandling(Action action) {
      try {
        action();
      } catch (HaltException e) {
        Console.WriteLine("[Program halted] " + e.Message);
        // This is unfriendly given that Dafny's C# compiler will
        // invoke the compiled main method directly,
        // so we might be exiting the whole Dafny process here.
        // That's the best we can do until Dafny main methods support
        // a return value though (https://github.com/dafny-lang/dafny/issues/2699).
        // If we just set Environment.ExitCode here, the Dafny CLI
        // will just override that with 0.
        Environment.Exit(1);
      }
    }

    public static Rune AddRunes(Rune left, Rune right) {
      return (Rune)(left.Value + right.Value);
    }

    public static Rune SubtractRunes(Rune left, Rune right) {
      return (Rune)(left.Value - right.Value);
    }

    public static uint Bv32ShiftLeft(uint a, int amount) {
      return 32 <= amount ? 0 : a << amount;
    }
    public static ulong Bv64ShiftLeft(ulong a, int amount) {
      return 64 <= amount ? 0 : a << amount;
    }

    public static uint Bv32ShiftRight(uint a, int amount) {
      return 32 <= amount ? 0 : a >> amount;
    }
    public static ulong Bv64ShiftRight(ulong a, int amount) {
      return 64 <= amount ? 0 : a >> amount;
    }
  }

  public class BigOrdinal {
    public static bool IsLimit(BigInteger ord) {
      return ord == 0;
    }
    public static bool IsSucc(BigInteger ord) {
      return 0 < ord;
    }
    public static BigInteger Offset(BigInteger ord) {
      return ord;
    }
    public static bool IsNat(BigInteger ord) {
      return true;  // at run time, every ORDINAL is a natural number
    }
  }

  public struct BigRational {
    public static readonly BigRational ZERO = new BigRational(0);

    // We need to deal with the special case "num == 0 && den == 0", because
    // that's what C#'s default struct constructor will produce for BigRational. :(
    // To deal with it, we ignore "den" when "num" is 0.
    public readonly BigInteger num, den;  // invariant 1 <= den || (num == 0 && den == 0)

    public override string ToString() {
      if (num.IsZero || den.IsOne) {
        return string.Format("{0}.0", num);
      } else if (DividesAPowerOf10(den, out var factor, out var log10)) {
        var n = num * factor;
        string sign;
        string digits;
        if (n.Sign < 0) {
          sign = "-"; digits = (-n).ToString();
        } else {
          sign = ""; digits = n.ToString();
        }
        if (log10 < digits.Length) {
          var digitCount = digits.Length - log10;
          return string.Format("{0}{1}.{2}", sign, digits.Substring(0, digitCount), digits.Substring(digitCount));
        } else {
          return string.Format("{0}0.{1}{2}", sign, new string('0', log10 - digits.Length), digits);
        }
      } else {
        return string.Format("({0}.0 / {1}.0)", num, den);
      }
    }
    public static bool IsPowerOf10(BigInteger x, out int log10) {
      log10 = 0;
      if (x.IsZero) {
        return false;
      }
      while (true) {  // invariant: x != 0 && x * 10^log10 == old(x)
        if (x.IsOne) {
          return true;
        } else if (x % 10 == 0) {
          log10++;
          x /= 10;
        } else {
          return false;
        }
      }
    }
    /// <summary>
    /// If this method return true, then
    ///     10^log10 == factor * i
    /// Otherwise, factor and log10 should not be used.
    /// </summary>
    public static bool DividesAPowerOf10(BigInteger i, out BigInteger factor, out int log10) {
      factor = BigInteger.One;
      log10 = 0;
      if (i <= 0) {
        return false;
      }

      BigInteger ten = 10;
      BigInteger five = 5;
      BigInteger two = 2;

      // invariant: 1 <= i && i * 10^log10 == factor * old(i)
      while (i % ten == 0) {
        i /= ten;
        log10++;
      }

      while (i % five == 0) {
        i /= five;
        factor *= two;
        log10++;
      }
      while (i % two == 0) {
        i /= two;
        factor *= five;
        log10++;
      }

      return i == BigInteger.One;
    }

    public BigRational(int n) {
      num = new BigInteger(n);
      den = BigInteger.One;
    }
    public BigRational(uint n) {
      num = new BigInteger(n);
      den = BigInteger.One;
    }
    public BigRational(long n) {
      num = new BigInteger(n);
      den = BigInteger.One;
    }
    public BigRational(ulong n) {
      num = new BigInteger(n);
      den = BigInteger.One;
    }
    public BigRational(BigInteger n, BigInteger d) {
      // requires 1 <= d
      num = n;
      den = d;
    }
    /// <summary>
    /// Construct an exact rational representation of a double value.
    /// Throw an exception on NaN or infinite values. Does not support
    /// subnormal values, though it would be possible to extend it to.
    /// </summary>
    public BigRational(double n) {
      if (Double.IsNaN(n)) {
        throw new ArgumentException("Can't convert NaN to a rational.");
      }
      if (Double.IsInfinity(n)) {
        throw new ArgumentException(
          "Can't convert +/- infinity to a rational.");
      }

      // Double-specific values
      const int exptBias = 1023;
      const ulong signMask = 0x8000000000000000;
      const ulong exptMask = 0x7FF0000000000000;
      const ulong mantMask = 0x000FFFFFFFFFFFFF;
      const int mantBits = 52;
      ulong bits = BitConverter.ToUInt64(BitConverter.GetBytes(n), 0);

      // Generic conversion
      bool isNeg = (bits & signMask) != 0;
      int expt = ((int)((bits & exptMask) >> mantBits)) - exptBias;
      var mant = (bits & mantMask);

      if (expt == -exptBias && mant != 0) {
        throw new ArgumentException(
          "Can't convert a subnormal value to a rational (yet).");
      }

      var one = BigInteger.One;
      var negFactor = isNeg ? BigInteger.Negate(one) : one;
      var two = new BigInteger(2);
      var exptBI = BigInteger.Pow(two, Math.Abs(expt));
      var twoToMantBits = BigInteger.Pow(two, mantBits);
      var mantNum = negFactor * (twoToMantBits + new BigInteger(mant));
      if (expt == -exptBias && mant == 0) {
        num = den = 0;
      } else if (expt < 0) {
        num = mantNum;
        den = twoToMantBits * exptBI;
      } else {
        num = exptBI * mantNum;
        den = twoToMantBits;
      }
    }
    public BigInteger ToBigInteger() {
      if (num.IsZero || den.IsOne) {
        return num;
      } else if (0 < num.Sign) {
        return num / den;
      } else {
        return (num - den + 1) / den;
      }
    }

    public bool IsInteger() {
      var floored = new BigRational(this.ToBigInteger(), BigInteger.One);
      return this == floored;
    }

    /// <summary>
    /// Returns values such that aa/dd == a and bb/dd == b.
    /// </summary>
    private static void Normalize(BigRational a, BigRational b, out BigInteger aa, out BigInteger bb, out BigInteger dd) {
      if (a.num.IsZero) {
        aa = a.num;
        bb = b.num;
        dd = b.den;
      } else if (b.num.IsZero) {
        aa = a.num;
        dd = a.den;
        bb = b.num;
      } else {
        var gcd = BigInteger.GreatestCommonDivisor(a.den, b.den);
        var xx = a.den / gcd;
        var yy = b.den / gcd;
        // We now have a == a.num / (xx * gcd) and b == b.num / (yy * gcd).
        aa = a.num * yy;
        bb = b.num * xx;
        dd = a.den * yy;
      }
    }
    public int CompareTo(BigRational that) {
      // simple things first
      int asign = this.num.Sign;
      int bsign = that.num.Sign;
      if (asign < 0 && 0 <= bsign) {
        return -1;
      } else if (asign <= 0 && 0 < bsign) {
        return -1;
      } else if (bsign < 0 && 0 <= asign) {
        return 1;
      } else if (bsign <= 0 && 0 < asign) {
        return 1;
      }

      Normalize(this, that, out var aa, out var bb, out var dd);
      return aa.CompareTo(bb);
    }
    public int Sign {
      get {
        return num.Sign;
      }
    }
    public override int GetHashCode() {
      return num.GetHashCode() + 29 * den.GetHashCode();
    }
    public override bool Equals(object obj) {
      if (obj is BigRational) {
        return this == (BigRational)obj;
      } else {
        return false;
      }
    }
    public static bool operator ==(BigRational a, BigRational b) {
      return a.CompareTo(b) == 0;
    }
    public static bool operator !=(BigRational a, BigRational b) {
      return a.CompareTo(b) != 0;
    }
    public static bool operator >(BigRational a, BigRational b) {
      return a.CompareTo(b) > 0;
    }
    public static bool operator >=(BigRational a, BigRational b) {
      return a.CompareTo(b) >= 0;
    }
    public static bool operator <(BigRational a, BigRational b) {
      return a.CompareTo(b) < 0;
    }
    public static bool operator <=(BigRational a, BigRational b) {
      return a.CompareTo(b) <= 0;
    }
    public static BigRational operator +(BigRational a, BigRational b) {
      Normalize(a, b, out var aa, out var bb, out var dd);
      return new BigRational(aa + bb, dd);
    }
    public static BigRational operator -(BigRational a, BigRational b) {
      Normalize(a, b, out var aa, out var bb, out var dd);
      return new BigRational(aa - bb, dd);
    }
    public static BigRational operator -(BigRational a) {
      return new BigRational(-a.num, a.den);
    }
    public static BigRational operator *(BigRational a, BigRational b) {
      return new BigRational(a.num * b.num, a.den * b.den);
    }
    public static BigRational operator /(BigRational a, BigRational b) {
      // Compute the reciprocal of b
      BigRational bReciprocal;
      if (0 < b.num.Sign) {
        bReciprocal = new BigRational(b.den, b.num);
      } else {
        // this is the case b.num < 0
        bReciprocal = new BigRational(-b.den, -b.num);
      }
      return a * bReciprocal;
    }
  }

  public class HaltException : Exception {
    public HaltException(object message) : base(message.ToString()) {
    }
  }
}
// Dafny program systemModulePopulator.dfy compiled into C#
// To recompile, you will need the libraries
//     System.Runtime.Numerics.dll System.Collections.Immutable.dll
// but the 'dotnet' tool in .NET should pick those up automatically.
// Optionally, you may want to include compiler switches like
//     /debug /nowarn:162,164,168,183,219,436,1717,1718

#if ISDAFNYRUNTIMELIB
using System;
using System.Numerics;
using System.Collections;
#endif
#if ISDAFNYRUNTIMELIB
namespace Dafny {
  internal class ArrayHelpers {
    public static T[] InitNewArray1<T>(T z, BigInteger size0) {
      int s0 = (int)size0;
      T[] a = new T[s0];
      for (int i0 = 0; i0 < s0; i0++) {
        a[i0] = z;
      }
      return a;
    }
    public static T[,] InitNewArray2<T>(T z, BigInteger size0, BigInteger size1) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      T[,] a = new T[s0,s1];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          a[i0,i1] = z;
        }
      }
      return a;
    }
    public static T[,,] InitNewArray3<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      T[,,] a = new T[s0,s1,s2];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            a[i0,i1,i2] = z;
          }
        }
      }
      return a;
    }
    public static T[,,,] InitNewArray4<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      T[,,,] a = new T[s0,s1,s2,s3];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              a[i0,i1,i2,i3] = z;
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,] InitNewArray5<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      T[,,,,] a = new T[s0,s1,s2,s3,s4];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                a[i0,i1,i2,i3,i4] = z;
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,] InitNewArray6<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      T[,,,,,] a = new T[s0,s1,s2,s3,s4,s5];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  a[i0,i1,i2,i3,i4,i5] = z;
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,] InitNewArray7<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      T[,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    a[i0,i1,i2,i3,i4,i5,i6] = z;
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,] InitNewArray8<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      T[,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      a[i0,i1,i2,i3,i4,i5,i6,i7] = z;
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,] InitNewArray9<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      T[,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        a[i0,i1,i2,i3,i4,i5,i6,i7,i8] = z;
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,] InitNewArray10<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      T[,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9] = z;
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,] InitNewArray11<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      T[,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10] = z;
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,,] InitNewArray12<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10, BigInteger size11) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      int s11 = (int)size11;
      T[,,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            for (int i11 = 0; i11 < s11; i11++) {
                              a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10,i11] = z;
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,,,] InitNewArray13<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10, BigInteger size11, BigInteger size12) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      int s11 = (int)size11;
      int s12 = (int)size12;
      T[,,,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,s12];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            for (int i11 = 0; i11 < s11; i11++) {
                              for (int i12 = 0; i12 < s12; i12++) {
                                a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10,i11,i12] = z;
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,,,,] InitNewArray14<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10, BigInteger size11, BigInteger size12, BigInteger size13) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      int s11 = (int)size11;
      int s12 = (int)size12;
      int s13 = (int)size13;
      T[,,,,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,s12,s13];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            for (int i11 = 0; i11 < s11; i11++) {
                              for (int i12 = 0; i12 < s12; i12++) {
                                for (int i13 = 0; i13 < s13; i13++) {
                                  a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10,i11,i12,i13] = z;
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,,,,,] InitNewArray15<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10, BigInteger size11, BigInteger size12, BigInteger size13, BigInteger size14) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      int s11 = (int)size11;
      int s12 = (int)size12;
      int s13 = (int)size13;
      int s14 = (int)size14;
      T[,,,,,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,s12,s13,s14];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            for (int i11 = 0; i11 < s11; i11++) {
                              for (int i12 = 0; i12 < s12; i12++) {
                                for (int i13 = 0; i13 < s13; i13++) {
                                  for (int i14 = 0; i14 < s14; i14++) {
                                    a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10,i11,i12,i13,i14] = z;
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
    public static T[,,,,,,,,,,,,,,,] InitNewArray16<T>(T z, BigInteger size0, BigInteger size1, BigInteger size2, BigInteger size3, BigInteger size4, BigInteger size5, BigInteger size6, BigInteger size7, BigInteger size8, BigInteger size9, BigInteger size10, BigInteger size11, BigInteger size12, BigInteger size13, BigInteger size14, BigInteger size15) {
      int s0 = (int)size0;
      int s1 = (int)size1;
      int s2 = (int)size2;
      int s3 = (int)size3;
      int s4 = (int)size4;
      int s5 = (int)size5;
      int s6 = (int)size6;
      int s7 = (int)size7;
      int s8 = (int)size8;
      int s9 = (int)size9;
      int s10 = (int)size10;
      int s11 = (int)size11;
      int s12 = (int)size12;
      int s13 = (int)size13;
      int s14 = (int)size14;
      int s15 = (int)size15;
      T[,,,,,,,,,,,,,,,] a = new T[s0,s1,s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,s12,s13,s14,s15];
      for (int i0 = 0; i0 < s0; i0++) {
        for (int i1 = 0; i1 < s1; i1++) {
          for (int i2 = 0; i2 < s2; i2++) {
            for (int i3 = 0; i3 < s3; i3++) {
              for (int i4 = 0; i4 < s4; i4++) {
                for (int i5 = 0; i5 < s5; i5++) {
                  for (int i6 = 0; i6 < s6; i6++) {
                    for (int i7 = 0; i7 < s7; i7++) {
                      for (int i8 = 0; i8 < s8; i8++) {
                        for (int i9 = 0; i9 < s9; i9++) {
                          for (int i10 = 0; i10 < s10; i10++) {
                            for (int i11 = 0; i11 < s11; i11++) {
                              for (int i12 = 0; i12 < s12; i12++) {
                                for (int i13 = 0; i13 < s13; i13++) {
                                  for (int i14 = 0; i14 < s14; i14++) {
                                    for (int i15 = 0; i15 < s15; i15++) {
                                      a[i0,i1,i2,i3,i4,i5,i6,i7,i8,i9,i10,i11,i12,i13,i14,i15] = z;
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
      return a;
    }
  }
} // end of namespace Dafny
internal static class FuncExtensions {
  public static Func<U, UResult> DowncastClone<T, TResult, U, UResult>(this Func<T, TResult> F, Func<U, T> ArgConv, Func<TResult, UResult> ResConv) {
    return arg => ResConv(F(ArgConv(arg)));
  }
  public static Func<UResult> DowncastClone<TResult, UResult>(this Func<TResult> F, Func<TResult, UResult> ResConv) {
    return () => ResConv(F());
  }
  public static Func<U1, U2, UResult> DowncastClone<T1, T2, TResult, U1, U2, UResult>(this Func<T1, T2, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<TResult, UResult> ResConv) {
    return (arg1, arg2) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2)));
  }
  public static Func<U1, U2, U3, UResult> DowncastClone<T1, T2, T3, TResult, U1, U2, U3, UResult>(this Func<T1, T2, T3, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3)));
  }
  public static Func<U1, U2, U3, U4, UResult> DowncastClone<T1, T2, T3, T4, TResult, U1, U2, U3, U4, UResult>(this Func<T1, T2, T3, T4, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4)));
  }
  public static Func<U1, U2, U3, U4, U5, UResult> DowncastClone<T1, T2, T3, T4, T5, TResult, U1, U2, U3, U4, U5, UResult>(this Func<T1, T2, T3, T4, T5, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, TResult, U1, U2, U3, U4, U5, U6, UResult>(this Func<T1, T2, T3, T4, T5, T6, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, TResult, U1, U2, U3, U4, U5, U6, U7, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, TResult, U1, U2, U3, U4, U5, U6, U7, U8, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<U12, T12> ArgConv12, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11), ArgConv12(arg12)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<U12, T12> ArgConv12, Func<U13, T13> ArgConv13, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11), ArgConv12(arg12), ArgConv13(arg13)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<U12, T12> ArgConv12, Func<U13, T13> ArgConv13, Func<U14, T14> ArgConv14, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11), ArgConv12(arg12), ArgConv13(arg13), ArgConv14(arg14)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<U12, T12> ArgConv12, Func<U13, T13> ArgConv13, Func<U14, T14> ArgConv14, Func<U15, T15> ArgConv15, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11), ArgConv12(arg12), ArgConv13(arg13), ArgConv14(arg14), ArgConv15(arg15)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, U16, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult, U1, U2, U3, U4, U5, U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, U16, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<U8, T8> ArgConv8, Func<U9, T9> ArgConv9, Func<U10, T10> ArgConv10, Func<U11, T11> ArgConv11, Func<U12, T12> ArgConv12, Func<U13, T13> ArgConv13, Func<U14, T14> ArgConv14, Func<U15, T15> ArgConv15, Func<U16, T16> ArgConv16, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7), ArgConv8(arg8), ArgConv9(arg9), ArgConv10(arg10), ArgConv11(arg11), ArgConv12(arg12), ArgConv13(arg13), ArgConv14(arg14), ArgConv15(arg15), ArgConv16(arg16)));
  }
}
// end of class FuncExtensions
#endif
namespace _System {

  public partial class nat {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _0_x = __source;
      return (_0_x).Sign != -1;
    }
  }

  public interface _ITuple2<out T0, out T1> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    _ITuple2<__T0, __T1> DowncastClone<__T0, __T1>(Func<T0, __T0> converter0, Func<T1, __T1> converter1);
  }
  public class Tuple2<T0, T1> : _ITuple2<T0, T1> {
    public readonly T0 __0;
    public readonly T1 __1;
    public Tuple2(T0 _0, T1 _1) {
      this.__0 = _0;
      this.__1 = _1;
    }
    public _ITuple2<__T0, __T1> DowncastClone<__T0, __T1>(Func<T0, __T0> converter0, Func<T1, __T1> converter1) {
      if (this is _ITuple2<__T0, __T1> dt) { return dt; }
      return new Tuple2<__T0, __T1>(converter0(__0), converter1(__1));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple2<T0, T1>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ")";
      return s;
    }
    public static _System._ITuple2<T0, T1> Default(T0 _default_T0, T1 _default_T1) {
      return create(_default_T0, _default_T1);
    }
    public static Dafny.TypeDescriptor<_System._ITuple2<T0, T1>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1) {
      return new Dafny.TypeDescriptor<_System._ITuple2<T0, T1>>(_System.Tuple2<T0, T1>.Default(_td_T0.Default(), _td_T1.Default()));
    }
    public static _ITuple2<T0, T1> create(T0 _0, T1 _1) {
      return new Tuple2<T0, T1>(_0, _1);
    }
    public static _ITuple2<T0, T1> create____hMake2(T0 _0, T1 _1) {
      return create(_0, _1);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
  }

  public interface _ITuple0 {
    _ITuple0 DowncastClone();
  }
  public class Tuple0 : _ITuple0 {
    public Tuple0() {
    }
    public _ITuple0 DowncastClone() {
      if (this is _ITuple0 dt) { return dt; }
      return new Tuple0();
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple0;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      return "()";
    }
    private static readonly _System._ITuple0 theDefault = create();
    public static _System._ITuple0 Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<_System._ITuple0> _TYPE = new Dafny.TypeDescriptor<_System._ITuple0>(_System.Tuple0.Default());
    public static Dafny.TypeDescriptor<_System._ITuple0> _TypeDescriptor() {
      return _TYPE;
    }
    public static _ITuple0 create() {
      return new Tuple0();
    }
    public static _ITuple0 create____hMake0() {
      return create();
    }
    public static System.Collections.Generic.IEnumerable<_ITuple0> AllSingletonConstructors {
      get {
        yield return Tuple0.create();
      }
    }
  }

  public interface _ITuple1<out T0> {
    T0 dtor__0 { get; }
    _ITuple1<__T0> DowncastClone<__T0>(Func<T0, __T0> converter0);
  }
  public class Tuple1<T0> : _ITuple1<T0> {
    public readonly T0 __0;
    public Tuple1(T0 _0) {
      this.__0 = _0;
    }
    public _ITuple1<__T0> DowncastClone<__T0>(Func<T0, __T0> converter0) {
      if (this is _ITuple1<__T0> dt) { return dt; }
      return new Tuple1<__T0>(converter0(__0));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple1<T0>;
      return oth != null && object.Equals(this.__0, oth.__0);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ")";
      return s;
    }
    public static _System._ITuple1<T0> Default(T0 _default_T0) {
      return create(_default_T0);
    }
    public static Dafny.TypeDescriptor<_System._ITuple1<T0>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0) {
      return new Dafny.TypeDescriptor<_System._ITuple1<T0>>(_System.Tuple1<T0>.Default(_td_T0.Default()));
    }
    public static _ITuple1<T0> create(T0 _0) {
      return new Tuple1<T0>(_0);
    }
    public static _ITuple1<T0> create____hMake1(T0 _0) {
      return create(_0);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
  }

  public interface _ITuple3<out T0, out T1, out T2> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    _ITuple3<__T0, __T1, __T2> DowncastClone<__T0, __T1, __T2>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2);
  }
  public class Tuple3<T0, T1, T2> : _ITuple3<T0, T1, T2> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public Tuple3(T0 _0, T1 _1, T2 _2) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
    }
    public _ITuple3<__T0, __T1, __T2> DowncastClone<__T0, __T1, __T2>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2) {
      if (this is _ITuple3<__T0, __T1, __T2> dt) { return dt; }
      return new Tuple3<__T0, __T1, __T2>(converter0(__0), converter1(__1), converter2(__2));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple3<T0, T1, T2>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ")";
      return s;
    }
    public static _System._ITuple3<T0, T1, T2> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2) {
      return create(_default_T0, _default_T1, _default_T2);
    }
    public static Dafny.TypeDescriptor<_System._ITuple3<T0, T1, T2>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2) {
      return new Dafny.TypeDescriptor<_System._ITuple3<T0, T1, T2>>(_System.Tuple3<T0, T1, T2>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default()));
    }
    public static _ITuple3<T0, T1, T2> create(T0 _0, T1 _1, T2 _2) {
      return new Tuple3<T0, T1, T2>(_0, _1, _2);
    }
    public static _ITuple3<T0, T1, T2> create____hMake3(T0 _0, T1 _1, T2 _2) {
      return create(_0, _1, _2);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
  }

  public interface _ITuple4<out T0, out T1, out T2, out T3> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    _ITuple4<__T0, __T1, __T2, __T3> DowncastClone<__T0, __T1, __T2, __T3>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3);
  }
  public class Tuple4<T0, T1, T2, T3> : _ITuple4<T0, T1, T2, T3> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public Tuple4(T0 _0, T1 _1, T2 _2, T3 _3) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
    }
    public _ITuple4<__T0, __T1, __T2, __T3> DowncastClone<__T0, __T1, __T2, __T3>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3) {
      if (this is _ITuple4<__T0, __T1, __T2, __T3> dt) { return dt; }
      return new Tuple4<__T0, __T1, __T2, __T3>(converter0(__0), converter1(__1), converter2(__2), converter3(__3));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple4<T0, T1, T2, T3>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ")";
      return s;
    }
    public static _System._ITuple4<T0, T1, T2, T3> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3);
    }
    public static Dafny.TypeDescriptor<_System._ITuple4<T0, T1, T2, T3>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3) {
      return new Dafny.TypeDescriptor<_System._ITuple4<T0, T1, T2, T3>>(_System.Tuple4<T0, T1, T2, T3>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default()));
    }
    public static _ITuple4<T0, T1, T2, T3> create(T0 _0, T1 _1, T2 _2, T3 _3) {
      return new Tuple4<T0, T1, T2, T3>(_0, _1, _2, _3);
    }
    public static _ITuple4<T0, T1, T2, T3> create____hMake4(T0 _0, T1 _1, T2 _2, T3 _3) {
      return create(_0, _1, _2, _3);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
  }

  public interface _ITuple5<out T0, out T1, out T2, out T3, out T4> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    _ITuple5<__T0, __T1, __T2, __T3, __T4> DowncastClone<__T0, __T1, __T2, __T3, __T4>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4);
  }
  public class Tuple5<T0, T1, T2, T3, T4> : _ITuple5<T0, T1, T2, T3, T4> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public Tuple5(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
    }
    public _ITuple5<__T0, __T1, __T2, __T3, __T4> DowncastClone<__T0, __T1, __T2, __T3, __T4>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4) {
      if (this is _ITuple5<__T0, __T1, __T2, __T3, __T4> dt) { return dt; }
      return new Tuple5<__T0, __T1, __T2, __T3, __T4>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple5<T0, T1, T2, T3, T4>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ")";
      return s;
    }
    public static _System._ITuple5<T0, T1, T2, T3, T4> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4);
    }
    public static Dafny.TypeDescriptor<_System._ITuple5<T0, T1, T2, T3, T4>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4) {
      return new Dafny.TypeDescriptor<_System._ITuple5<T0, T1, T2, T3, T4>>(_System.Tuple5<T0, T1, T2, T3, T4>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default()));
    }
    public static _ITuple5<T0, T1, T2, T3, T4> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4) {
      return new Tuple5<T0, T1, T2, T3, T4>(_0, _1, _2, _3, _4);
    }
    public static _ITuple5<T0, T1, T2, T3, T4> create____hMake5(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4) {
      return create(_0, _1, _2, _3, _4);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
  }

  public interface _ITuple6<out T0, out T1, out T2, out T3, out T4, out T5> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    _ITuple6<__T0, __T1, __T2, __T3, __T4, __T5> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5);
  }
  public class Tuple6<T0, T1, T2, T3, T4, T5> : _ITuple6<T0, T1, T2, T3, T4, T5> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public Tuple6(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
    }
    public _ITuple6<__T0, __T1, __T2, __T3, __T4, __T5> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5) {
      if (this is _ITuple6<__T0, __T1, __T2, __T3, __T4, __T5> dt) { return dt; }
      return new Tuple6<__T0, __T1, __T2, __T3, __T4, __T5>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple6<T0, T1, T2, T3, T4, T5>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ")";
      return s;
    }
    public static _System._ITuple6<T0, T1, T2, T3, T4, T5> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5);
    }
    public static Dafny.TypeDescriptor<_System._ITuple6<T0, T1, T2, T3, T4, T5>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5) {
      return new Dafny.TypeDescriptor<_System._ITuple6<T0, T1, T2, T3, T4, T5>>(_System.Tuple6<T0, T1, T2, T3, T4, T5>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default()));
    }
    public static _ITuple6<T0, T1, T2, T3, T4, T5> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5) {
      return new Tuple6<T0, T1, T2, T3, T4, T5>(_0, _1, _2, _3, _4, _5);
    }
    public static _ITuple6<T0, T1, T2, T3, T4, T5> create____hMake6(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5) {
      return create(_0, _1, _2, _3, _4, _5);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
  }

  public interface _ITuple7<out T0, out T1, out T2, out T3, out T4, out T5, out T6> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    _ITuple7<__T0, __T1, __T2, __T3, __T4, __T5, __T6> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6);
  }
  public class Tuple7<T0, T1, T2, T3, T4, T5, T6> : _ITuple7<T0, T1, T2, T3, T4, T5, T6> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public Tuple7(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
    }
    public _ITuple7<__T0, __T1, __T2, __T3, __T4, __T5, __T6> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6) {
      if (this is _ITuple7<__T0, __T1, __T2, __T3, __T4, __T5, __T6> dt) { return dt; }
      return new Tuple7<__T0, __T1, __T2, __T3, __T4, __T5, __T6>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple7<T0, T1, T2, T3, T4, T5, T6>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ")";
      return s;
    }
    public static _System._ITuple7<T0, T1, T2, T3, T4, T5, T6> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6);
    }
    public static Dafny.TypeDescriptor<_System._ITuple7<T0, T1, T2, T3, T4, T5, T6>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6) {
      return new Dafny.TypeDescriptor<_System._ITuple7<T0, T1, T2, T3, T4, T5, T6>>(_System.Tuple7<T0, T1, T2, T3, T4, T5, T6>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default()));
    }
    public static _ITuple7<T0, T1, T2, T3, T4, T5, T6> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6) {
      return new Tuple7<T0, T1, T2, T3, T4, T5, T6>(_0, _1, _2, _3, _4, _5, _6);
    }
    public static _ITuple7<T0, T1, T2, T3, T4, T5, T6> create____hMake7(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6) {
      return create(_0, _1, _2, _3, _4, _5, _6);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
  }

  public interface _ITuple8<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    _ITuple8<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7);
  }
  public class Tuple8<T0, T1, T2, T3, T4, T5, T6, T7> : _ITuple8<T0, T1, T2, T3, T4, T5, T6, T7> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public Tuple8(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
    }
    public _ITuple8<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7) {
      if (this is _ITuple8<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7> dt) { return dt; }
      return new Tuple8<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple8<T0, T1, T2, T3, T4, T5, T6, T7>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ")";
      return s;
    }
    public static _System._ITuple8<T0, T1, T2, T3, T4, T5, T6, T7> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7);
    }
    public static Dafny.TypeDescriptor<_System._ITuple8<T0, T1, T2, T3, T4, T5, T6, T7>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7) {
      return new Dafny.TypeDescriptor<_System._ITuple8<T0, T1, T2, T3, T4, T5, T6, T7>>(_System.Tuple8<T0, T1, T2, T3, T4, T5, T6, T7>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default()));
    }
    public static _ITuple8<T0, T1, T2, T3, T4, T5, T6, T7> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7) {
      return new Tuple8<T0, T1, T2, T3, T4, T5, T6, T7>(_0, _1, _2, _3, _4, _5, _6, _7);
    }
    public static _ITuple8<T0, T1, T2, T3, T4, T5, T6, T7> create____hMake8(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
  }

  public interface _ITuple9<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    _ITuple9<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8);
  }
  public class Tuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8> : _ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public Tuple9(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
    }
    public _ITuple9<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8) {
      if (this is _ITuple9<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8> dt) { return dt; }
      return new Tuple9<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ")";
      return s;
    }
    public static _System._ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8);
    }
    public static Dafny.TypeDescriptor<_System._ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8) {
      return new Dafny.TypeDescriptor<_System._ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8>>(_System.Tuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default()));
    }
    public static _ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8) {
      return new Tuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8>(_0, _1, _2, _3, _4, _5, _6, _7, _8);
    }
    public static _ITuple9<T0, T1, T2, T3, T4, T5, T6, T7, T8> create____hMake9(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
  }

  public interface _ITuple10<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    _ITuple10<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9);
  }
  public class Tuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : _ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public Tuple10(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
    }
    public _ITuple10<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9) {
      if (this is _ITuple10<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9> dt) { return dt; }
      return new Tuple10<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ")";
      return s;
    }
    public static _System._ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9);
    }
    public static Dafny.TypeDescriptor<_System._ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9) {
      return new Dafny.TypeDescriptor<_System._ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>>(_System.Tuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default()));
    }
    public static _ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9) {
      return new Tuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9);
    }
    public static _ITuple10<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> create____hMake10(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
  }

  public interface _ITuple11<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    _ITuple11<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10);
  }
  public class Tuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : _ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public Tuple11(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
    }
    public _ITuple11<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10) {
      if (this is _ITuple11<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10> dt) { return dt; }
      return new Tuple11<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ")";
      return s;
    }
    public static _System._ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10);
    }
    public static Dafny.TypeDescriptor<_System._ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10) {
      return new Dafny.TypeDescriptor<_System._ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>(_System.Tuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default()));
    }
    public static _ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10) {
      return new Tuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10);
    }
    public static _ITuple11<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> create____hMake11(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
  }

  public interface _ITuple12<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    _ITuple12<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11);
  }
  public class Tuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : _ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public Tuple12(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
    }
    public _ITuple12<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11) {
      if (this is _ITuple12<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11> dt) { return dt; }
      return new Tuple12<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ")";
      return s;
    }
    public static _System._ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11);
    }
    public static Dafny.TypeDescriptor<_System._ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11) {
      return new Dafny.TypeDescriptor<_System._ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>(_System.Tuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default()));
    }
    public static _ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11) {
      return new Tuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11);
    }
    public static _ITuple12<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> create____hMake12(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
  }

  public interface _ITuple13<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    _ITuple13<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12);
  }
  public class Tuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : _ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public Tuple13(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
    }
    public _ITuple13<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12) {
      if (this is _ITuple13<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12> dt) { return dt; }
      return new Tuple13<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ")";
      return s;
    }
    public static _System._ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12);
    }
    public static Dafny.TypeDescriptor<_System._ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12) {
      return new Dafny.TypeDescriptor<_System._ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>(_System.Tuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default()));
    }
    public static _ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12) {
      return new Tuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12);
    }
    public static _ITuple13<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> create____hMake13(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
  }

  public interface _ITuple14<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    _ITuple14<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13);
  }
  public class Tuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> : _ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public Tuple14(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
    }
    public _ITuple14<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13) {
      if (this is _ITuple14<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13> dt) { return dt; }
      return new Tuple14<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ")";
      return s;
    }
    public static _System._ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13);
    }
    public static Dafny.TypeDescriptor<_System._ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13) {
      return new Dafny.TypeDescriptor<_System._ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>(_System.Tuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default()));
    }
    public static _ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13) {
      return new Tuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13);
    }
    public static _ITuple14<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> create____hMake14(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
  }

  public interface _ITuple15<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    _ITuple15<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14);
  }
  public class Tuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> : _ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public Tuple15(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
    }
    public _ITuple15<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14) {
      if (this is _ITuple15<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14> dt) { return dt; }
      return new Tuple15<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ")";
      return s;
    }
    public static _System._ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14);
    }
    public static Dafny.TypeDescriptor<_System._ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14) {
      return new Dafny.TypeDescriptor<_System._ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>(_System.Tuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default()));
    }
    public static _ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14) {
      return new Tuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14);
    }
    public static _ITuple15<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> create____hMake15(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
  }

  public interface _ITuple16<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14, out T15> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    T15 dtor__15 { get; }
    _ITuple16<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15);
  }
  public class Tuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> : _ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public readonly T15 __15;
    public Tuple16(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
      this.__15 = _15;
    }
    public _ITuple16<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15) {
      if (this is _ITuple16<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15> dt) { return dt; }
      return new Tuple16<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14), converter15(__15));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14) && object.Equals(this.__15, oth.__15);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__15));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__15);
      s += ")";
      return s;
    }
    public static _System._ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14, T15 _default_T15) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14, _default_T15);
    }
    public static Dafny.TypeDescriptor<_System._ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14, Dafny.TypeDescriptor<T15> _td_T15) {
      return new Dafny.TypeDescriptor<_System._ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>(_System.Tuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default(), _td_T15.Default()));
    }
    public static _ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15) {
      return new Tuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15);
    }
    public static _ITuple16<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> create____hMake16(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
    public T15 dtor__15 {
      get {
        return this.__15;
      }
    }
  }

  public interface _ITuple17<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14, out T15, out T16> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    T15 dtor__15 { get; }
    T16 dtor__16 { get; }
    _ITuple17<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16);
  }
  public class Tuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> : _ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public readonly T15 __15;
    public readonly T16 __16;
    public Tuple17(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
      this.__15 = _15;
      this.__16 = _16;
    }
    public _ITuple17<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16) {
      if (this is _ITuple17<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16> dt) { return dt; }
      return new Tuple17<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14), converter15(__15), converter16(__16));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14) && object.Equals(this.__15, oth.__15) && object.Equals(this.__16, oth.__16);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__15));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__16));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__15);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__16);
      s += ")";
      return s;
    }
    public static _System._ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14, T15 _default_T15, T16 _default_T16) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14, _default_T15, _default_T16);
    }
    public static Dafny.TypeDescriptor<_System._ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14, Dafny.TypeDescriptor<T15> _td_T15, Dafny.TypeDescriptor<T16> _td_T16) {
      return new Dafny.TypeDescriptor<_System._ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>>(_System.Tuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default(), _td_T15.Default(), _td_T16.Default()));
    }
    public static _ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16) {
      return new Tuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16);
    }
    public static _ITuple17<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> create____hMake17(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
    public T15 dtor__15 {
      get {
        return this.__15;
      }
    }
    public T16 dtor__16 {
      get {
        return this.__16;
      }
    }
  }

  public interface _ITuple18<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14, out T15, out T16, out T17> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    T15 dtor__15 { get; }
    T16 dtor__16 { get; }
    T17 dtor__17 { get; }
    _ITuple18<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17);
  }
  public class Tuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17> : _ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public readonly T15 __15;
    public readonly T16 __16;
    public readonly T17 __17;
    public Tuple18(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
      this.__15 = _15;
      this.__16 = _16;
      this.__17 = _17;
    }
    public _ITuple18<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17) {
      if (this is _ITuple18<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17> dt) { return dt; }
      return new Tuple18<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14), converter15(__15), converter16(__16), converter17(__17));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14) && object.Equals(this.__15, oth.__15) && object.Equals(this.__16, oth.__16) && object.Equals(this.__17, oth.__17);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__15));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__16));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__17));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__15);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__16);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__17);
      s += ")";
      return s;
    }
    public static _System._ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14, T15 _default_T15, T16 _default_T16, T17 _default_T17) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14, _default_T15, _default_T16, _default_T17);
    }
    public static Dafny.TypeDescriptor<_System._ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14, Dafny.TypeDescriptor<T15> _td_T15, Dafny.TypeDescriptor<T16> _td_T16, Dafny.TypeDescriptor<T17> _td_T17) {
      return new Dafny.TypeDescriptor<_System._ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>>(_System.Tuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default(), _td_T15.Default(), _td_T16.Default(), _td_T17.Default()));
    }
    public static _ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17) {
      return new Tuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17);
    }
    public static _ITuple18<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17> create____hMake18(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
    public T15 dtor__15 {
      get {
        return this.__15;
      }
    }
    public T16 dtor__16 {
      get {
        return this.__16;
      }
    }
    public T17 dtor__17 {
      get {
        return this.__17;
      }
    }
  }

  public interface _ITuple19<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14, out T15, out T16, out T17, out T18> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    T15 dtor__15 { get; }
    T16 dtor__16 { get; }
    T17 dtor__17 { get; }
    T18 dtor__18 { get; }
    _ITuple19<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17, Func<T18, __T18> converter18);
  }
  public class Tuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18> : _ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public readonly T15 __15;
    public readonly T16 __16;
    public readonly T17 __17;
    public readonly T18 __18;
    public Tuple19(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
      this.__15 = _15;
      this.__16 = _16;
      this.__17 = _17;
      this.__18 = _18;
    }
    public _ITuple19<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17, Func<T18, __T18> converter18) {
      if (this is _ITuple19<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18> dt) { return dt; }
      return new Tuple19<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14), converter15(__15), converter16(__16), converter17(__17), converter18(__18));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14) && object.Equals(this.__15, oth.__15) && object.Equals(this.__16, oth.__16) && object.Equals(this.__17, oth.__17) && object.Equals(this.__18, oth.__18);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__15));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__16));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__17));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__18));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__15);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__16);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__17);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__18);
      s += ")";
      return s;
    }
    public static _System._ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14, T15 _default_T15, T16 _default_T16, T17 _default_T17, T18 _default_T18) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14, _default_T15, _default_T16, _default_T17, _default_T18);
    }
    public static Dafny.TypeDescriptor<_System._ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14, Dafny.TypeDescriptor<T15> _td_T15, Dafny.TypeDescriptor<T16> _td_T16, Dafny.TypeDescriptor<T17> _td_T17, Dafny.TypeDescriptor<T18> _td_T18) {
      return new Dafny.TypeDescriptor<_System._ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>>(_System.Tuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default(), _td_T15.Default(), _td_T16.Default(), _td_T17.Default(), _td_T18.Default()));
    }
    public static _ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18) {
      return new Tuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17, _18);
    }
    public static _ITuple19<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18> create____hMake19(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17, _18);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
    public T15 dtor__15 {
      get {
        return this.__15;
      }
    }
    public T16 dtor__16 {
      get {
        return this.__16;
      }
    }
    public T17 dtor__17 {
      get {
        return this.__17;
      }
    }
    public T18 dtor__18 {
      get {
        return this.__18;
      }
    }
  }

  public interface _ITuple20<out T0, out T1, out T2, out T3, out T4, out T5, out T6, out T7, out T8, out T9, out T10, out T11, out T12, out T13, out T14, out T15, out T16, out T17, out T18, out T19> {
    T0 dtor__0 { get; }
    T1 dtor__1 { get; }
    T2 dtor__2 { get; }
    T3 dtor__3 { get; }
    T4 dtor__4 { get; }
    T5 dtor__5 { get; }
    T6 dtor__6 { get; }
    T7 dtor__7 { get; }
    T8 dtor__8 { get; }
    T9 dtor__9 { get; }
    T10 dtor__10 { get; }
    T11 dtor__11 { get; }
    T12 dtor__12 { get; }
    T13 dtor__13 { get; }
    T14 dtor__14 { get; }
    T15 dtor__15 { get; }
    T16 dtor__16 { get; }
    T17 dtor__17 { get; }
    T18 dtor__18 { get; }
    T19 dtor__19 { get; }
    _ITuple20<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17, Func<T18, __T18> converter18, Func<T19, __T19> converter19);
  }
  public class Tuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19> : _ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19> {
    public readonly T0 __0;
    public readonly T1 __1;
    public readonly T2 __2;
    public readonly T3 __3;
    public readonly T4 __4;
    public readonly T5 __5;
    public readonly T6 __6;
    public readonly T7 __7;
    public readonly T8 __8;
    public readonly T9 __9;
    public readonly T10 __10;
    public readonly T11 __11;
    public readonly T12 __12;
    public readonly T13 __13;
    public readonly T14 __14;
    public readonly T15 __15;
    public readonly T16 __16;
    public readonly T17 __17;
    public readonly T18 __18;
    public readonly T19 __19;
    public Tuple20(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18, T19 _19) {
      this.__0 = _0;
      this.__1 = _1;
      this.__2 = _2;
      this.__3 = _3;
      this.__4 = _4;
      this.__5 = _5;
      this.__6 = _6;
      this.__7 = _7;
      this.__8 = _8;
      this.__9 = _9;
      this.__10 = _10;
      this.__11 = _11;
      this.__12 = _12;
      this.__13 = _13;
      this.__14 = _14;
      this.__15 = _15;
      this.__16 = _16;
      this.__17 = _17;
      this.__18 = _18;
      this.__19 = _19;
    }
    public _ITuple20<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19> DowncastClone<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19>(Func<T0, __T0> converter0, Func<T1, __T1> converter1, Func<T2, __T2> converter2, Func<T3, __T3> converter3, Func<T4, __T4> converter4, Func<T5, __T5> converter5, Func<T6, __T6> converter6, Func<T7, __T7> converter7, Func<T8, __T8> converter8, Func<T9, __T9> converter9, Func<T10, __T10> converter10, Func<T11, __T11> converter11, Func<T12, __T12> converter12, Func<T13, __T13> converter13, Func<T14, __T14> converter14, Func<T15, __T15> converter15, Func<T16, __T16> converter16, Func<T17, __T17> converter17, Func<T18, __T18> converter18, Func<T19, __T19> converter19) {
      if (this is _ITuple20<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19> dt) { return dt; }
      return new Tuple20<__T0, __T1, __T2, __T3, __T4, __T5, __T6, __T7, __T8, __T9, __T10, __T11, __T12, __T13, __T14, __T15, __T16, __T17, __T18, __T19>(converter0(__0), converter1(__1), converter2(__2), converter3(__3), converter4(__4), converter5(__5), converter6(__6), converter7(__7), converter8(__8), converter9(__9), converter10(__10), converter11(__11), converter12(__12), converter13(__13), converter14(__14), converter15(__15), converter16(__16), converter17(__17), converter18(__18), converter19(__19));
    }
    public override bool Equals(object other) {
      var oth = other as _System.Tuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19>;
      return oth != null && object.Equals(this.__0, oth.__0) && object.Equals(this.__1, oth.__1) && object.Equals(this.__2, oth.__2) && object.Equals(this.__3, oth.__3) && object.Equals(this.__4, oth.__4) && object.Equals(this.__5, oth.__5) && object.Equals(this.__6, oth.__6) && object.Equals(this.__7, oth.__7) && object.Equals(this.__8, oth.__8) && object.Equals(this.__9, oth.__9) && object.Equals(this.__10, oth.__10) && object.Equals(this.__11, oth.__11) && object.Equals(this.__12, oth.__12) && object.Equals(this.__13, oth.__13) && object.Equals(this.__14, oth.__14) && object.Equals(this.__15, oth.__15) && object.Equals(this.__16, oth.__16) && object.Equals(this.__17, oth.__17) && object.Equals(this.__18, oth.__18) && object.Equals(this.__19, oth.__19);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__0));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__1));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__2));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__3));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__4));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__5));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__6));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__7));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__8));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__9));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__10));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__11));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__12));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__13));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__14));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__15));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__16));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__17));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__18));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this.__19));
      return (int) hash;
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += Dafny.Helpers.ToString(this.__0);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__1);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__2);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__3);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__4);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__5);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__6);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__7);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__8);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__9);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__10);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__11);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__12);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__13);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__14);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__15);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__16);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__17);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__18);
      s += ", ";
      s += Dafny.Helpers.ToString(this.__19);
      s += ")";
      return s;
    }
    public static _System._ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19> Default(T0 _default_T0, T1 _default_T1, T2 _default_T2, T3 _default_T3, T4 _default_T4, T5 _default_T5, T6 _default_T6, T7 _default_T7, T8 _default_T8, T9 _default_T9, T10 _default_T10, T11 _default_T11, T12 _default_T12, T13 _default_T13, T14 _default_T14, T15 _default_T15, T16 _default_T16, T17 _default_T17, T18 _default_T18, T19 _default_T19) {
      return create(_default_T0, _default_T1, _default_T2, _default_T3, _default_T4, _default_T5, _default_T6, _default_T7, _default_T8, _default_T9, _default_T10, _default_T11, _default_T12, _default_T13, _default_T14, _default_T15, _default_T16, _default_T17, _default_T18, _default_T19);
    }
    public static Dafny.TypeDescriptor<_System._ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19>> _TypeDescriptor(Dafny.TypeDescriptor<T0> _td_T0, Dafny.TypeDescriptor<T1> _td_T1, Dafny.TypeDescriptor<T2> _td_T2, Dafny.TypeDescriptor<T3> _td_T3, Dafny.TypeDescriptor<T4> _td_T4, Dafny.TypeDescriptor<T5> _td_T5, Dafny.TypeDescriptor<T6> _td_T6, Dafny.TypeDescriptor<T7> _td_T7, Dafny.TypeDescriptor<T8> _td_T8, Dafny.TypeDescriptor<T9> _td_T9, Dafny.TypeDescriptor<T10> _td_T10, Dafny.TypeDescriptor<T11> _td_T11, Dafny.TypeDescriptor<T12> _td_T12, Dafny.TypeDescriptor<T13> _td_T13, Dafny.TypeDescriptor<T14> _td_T14, Dafny.TypeDescriptor<T15> _td_T15, Dafny.TypeDescriptor<T16> _td_T16, Dafny.TypeDescriptor<T17> _td_T17, Dafny.TypeDescriptor<T18> _td_T18, Dafny.TypeDescriptor<T19> _td_T19) {
      return new Dafny.TypeDescriptor<_System._ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19>>(_System.Tuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19>.Default(_td_T0.Default(), _td_T1.Default(), _td_T2.Default(), _td_T3.Default(), _td_T4.Default(), _td_T5.Default(), _td_T6.Default(), _td_T7.Default(), _td_T8.Default(), _td_T9.Default(), _td_T10.Default(), _td_T11.Default(), _td_T12.Default(), _td_T13.Default(), _td_T14.Default(), _td_T15.Default(), _td_T16.Default(), _td_T17.Default(), _td_T18.Default(), _td_T19.Default()));
    }
    public static _ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19> create(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18, T19 _19) {
      return new Tuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19>(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17, _18, _19);
    }
    public static _ITuple20<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19> create____hMake20(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8, T9 _9, T10 _10, T11 _11, T12 _12, T13 _13, T14 _14, T15 _15, T16 _16, T17 _17, T18 _18, T19 _19) {
      return create(_0, _1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17, _18, _19);
    }
    public T0 dtor__0 {
      get {
        return this.__0;
      }
    }
    public T1 dtor__1 {
      get {
        return this.__1;
      }
    }
    public T2 dtor__2 {
      get {
        return this.__2;
      }
    }
    public T3 dtor__3 {
      get {
        return this.__3;
      }
    }
    public T4 dtor__4 {
      get {
        return this.__4;
      }
    }
    public T5 dtor__5 {
      get {
        return this.__5;
      }
    }
    public T6 dtor__6 {
      get {
        return this.__6;
      }
    }
    public T7 dtor__7 {
      get {
        return this.__7;
      }
    }
    public T8 dtor__8 {
      get {
        return this.__8;
      }
    }
    public T9 dtor__9 {
      get {
        return this.__9;
      }
    }
    public T10 dtor__10 {
      get {
        return this.__10;
      }
    }
    public T11 dtor__11 {
      get {
        return this.__11;
      }
    }
    public T12 dtor__12 {
      get {
        return this.__12;
      }
    }
    public T13 dtor__13 {
      get {
        return this.__13;
      }
    }
    public T14 dtor__14 {
      get {
        return this.__14;
      }
    }
    public T15 dtor__15 {
      get {
        return this.__15;
      }
    }
    public T16 dtor__16 {
      get {
        return this.__16;
      }
    }
    public T17 dtor__17 {
      get {
        return this.__17;
      }
    }
    public T18 dtor__18 {
      get {
        return this.__18;
      }
    }
    public T19 dtor__19 {
      get {
        return this.__19;
      }
    }
  }
} // end of namespace _System
namespace Std.Concurrent {
    using System.Collections.Concurrent;
    using Std.Wrappers;

    public class MutableMap<K, V> {

        private ConcurrentDictionary<K, V> map;

        public MutableMap() {
            map = new ConcurrentDictionary<K, V>();
        }

        public void __ctor(bool bytesKeys) { }

        public Dafny.ISet<K> Keys() {
            return Dafny.Set<K>.FromCollection(map.Keys);
        }

        public bool HasKey(K k) {
            return map.ContainsKey(k);
        }

        public Dafny.ISet<V> Values() {
            return Dafny.Set<V>.FromCollection(map.Values);
        }
        public Dafny.ISet<_System._ITuple2<K, V>> Items() {
            System.Collections.Generic.IEnumerable<_System._ITuple2<K, V>> ToEnumerable(System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<K, V>> enumerator) {
                while (enumerator.MoveNext())
                    yield return  _System.Tuple2<K, V>.create(enumerator.Current.Key, enumerator.Current.Value);
            }

            return Dafny.Set<_System._ITuple2<K, V>>.FromCollection(ToEnumerable(map.GetEnumerator()));
        }

        public void Put(K k, V v) {
            map.AddOrUpdate(k, v, ((key, oldValue) => v));
        }

        public _IOption<V> Get(K k) {
            V v;
            if (map.TryGetValue(k, out v)) {
                return Option<V>.create_Some(v);
            } else {
                return Option<V>.create_None();
            }
        }

        public void Remove(K k) {
            map.TryRemove(k, out _);
        }

        public System.Numerics.BigInteger Size() {
            return new System.Numerics.BigInteger(map.Count);
        }
    }

    public class AtomicBox<T> {

        private T val;
        private Lock l;

        public AtomicBox() {
            l = new Lock();
        }

        public void __ctor(T t) {
          val = t;
        }

        public void Put(T t) {
            l.__Lock();
            val = t;
            l.Unlock();
        }

        public T Get() {
            l.__Lock();
            var r = val;
            l.Unlock();
            return r;
        }
    }

    public class Lock {

        private static System.Threading.Mutex mut = new System.Threading.Mutex();

        public void __ctor() {
        }

        public void __Lock() {
            mut.WaitOne();
        }

        public void Unlock() {
            mut.ReleaseMutex();
        }
    }
}
/*******************************************************************************
*  Copyright by the contributors to the Dafny Project
*  SPDX-License-Identifier: MIT
*******************************************************************************/

namespace Std.FileIOInternalExterns {
  using System;
  using System.IO;

  using Dafny;

  public class __default {
    /// <summary>
    /// Attempts to read all bytes from the file at the given path, and outputs the following values:
    /// <list>
    ///   <item>
    ///     <term>isError</term>
    ///     <description>
    ///       true iff an exception was thrown during path string conversion or when reading the file
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>bytesRead</term>
    ///     <description>
    ///       the sequence of bytes read from the file, or an empty sequence if <c>isError</c> is true
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>errorMsg</term>
    ///     <description>
    ///       the error message of the thrown exception if <c>isError</c> is true, or an empty sequence otherwise
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// We output these values individually because Result is not defined in the runtime but instead in library code.
    /// It is the responsibility of library code to construct an equivalent Result value.
    /// </summary>
    public static void INTERNAL__ReadBytesFromFile(ISequence<Dafny.Rune> path, out bool isError, out ISequence<byte> bytesRead,
      out ISequence<Dafny.Rune> errorMsg) {
      isError = true;
      bytesRead = Sequence<byte>.Empty;
      errorMsg = Sequence<Rune>.Empty;
      try {
        bytesRead = Helpers.SeqFromArray(File.ReadAllBytes(path?.ToVerbatimString(false)));
        isError = false;
      } catch (Exception e) {
        errorMsg = Sequence<Rune>.UnicodeFromString(e.ToString());
      }
    }

    /// <summary>
    /// Attempts to write all given bytes to the file at the given path, creating nonexistent parent directories as necessary,
    /// and outputs the following values:
    /// <list>
    ///   <item>
    ///     <term>isError</term>
    ///     <description>
    ///       true iff an exception was thrown during path string conversion or when writing to the file
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>errorMsg</term>
    ///     <description>
    ///       the error message of the thrown exception if <c>isError</c> is true, or an empty sequence otherwise
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// We output these values individually because Result is not defined in the runtime but instead in library code.
    /// It is the responsibility of library code to construct an equivalent Result value.
    /// </summary>
    public static void INTERNAL__WriteBytesToFile(ISequence<Dafny.Rune> path, ISequence<byte> bytes, out bool isError, out ISequence<Dafny.Rune> errorMsg) {
      isError = true;
      errorMsg = Sequence<Rune>.Empty;
      try {
        string pathStr = path?.ToVerbatimString(false);
        CreateParentDirs(pathStr);
        File.WriteAllBytes(pathStr, bytes.CloneAsArray());
        isError = false;
      } catch (Exception e) {
        errorMsg = Sequence<Rune>.UnicodeFromString(e.ToString());
      }
    }

    /// <summary>
    /// Creates the nonexistent parent directory(-ies) of the given path.
    /// </summary>
    private static void CreateParentDirs(string path) {
      string parentDir = Path.GetDirectoryName(Path.GetFullPath(path));
      Directory.CreateDirectory(parentDir);
    }
  }
}
namespace Dafny {
  internal class ArrayHelpers {
    public static T[] InitNewArray1<T>(T z, BigInteger size0) {
      int s0 = (int)size0;
      T[] a = new T[s0];
      for (int i0 = 0; i0 < s0; i0++) {
        a[i0] = z;
      }
      return a;
    }
  }
} // end of namespace Dafny
internal static class FuncExtensions {
  public static Func<U, UResult> DowncastClone<T, TResult, U, UResult>(this Func<T, TResult> F, Func<U, T> ArgConv, Func<TResult, UResult> ResConv) {
    return arg => ResConv(F(ArgConv(arg)));
  }
  public static Func<UResult> DowncastClone<TResult, UResult>(this Func<TResult> F, Func<TResult, UResult> ResConv) {
    return () => ResConv(F());
  }
  public static Func<U1, U2, UResult> DowncastClone<T1, T2, TResult, U1, U2, UResult>(this Func<T1, T2, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<TResult, UResult> ResConv) {
    return (arg1, arg2) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2)));
  }
  public static Func<U1, U2, U3, UResult> DowncastClone<T1, T2, T3, TResult, U1, U2, U3, UResult>(this Func<T1, T2, T3, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3)));
  }
  public static Func<U1, U2, U3, U4, UResult> DowncastClone<T1, T2, T3, T4, TResult, U1, U2, U3, U4, UResult>(this Func<T1, T2, T3, T4, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4)));
  }
  public static Func<U1, U2, U3, U4, U5, UResult> DowncastClone<T1, T2, T3, T4, T5, TResult, U1, U2, U3, U4, U5, UResult>(this Func<T1, T2, T3, T4, T5, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5)));
  }
  public static Func<U1, U2, U3, U4, U5, U6, U7, UResult> DowncastClone<T1, T2, T3, T4, T5, T6, T7, TResult, U1, U2, U3, U4, U5, U6, U7, UResult>(this Func<T1, T2, T3, T4, T5, T6, T7, TResult> F, Func<U1, T1> ArgConv1, Func<U2, T2> ArgConv2, Func<U3, T3> ArgConv3, Func<U4, T4> ArgConv4, Func<U5, T5> ArgConv5, Func<U6, T6> ArgConv6, Func<U7, T7> ArgConv7, Func<TResult, UResult> ResConv) {
    return (arg1, arg2, arg3, arg4, arg5, arg6, arg7) => ResConv(F(ArgConv1(arg1), ArgConv2(arg2), ArgConv3(arg3), ArgConv4(arg4), ArgConv5(arg5), ArgConv6(arg6), ArgConv7(arg7)));
  }
}
// end of class FuncExtensions
namespace Std.Wrappers {

  public partial class __default {
    public static Std.Wrappers._IOutcomeResult<__E> Need<__E>(bool condition, __E error)
    {
      if (condition) {
        return Std.Wrappers.OutcomeResult<__E>.create_Pass_k();
      } else {
        return Std.Wrappers.OutcomeResult<__E>.create_Fail_k(error);
      }
    }
  }

  public interface _IOption<out T> {
    bool is_None { get; }
    bool is_Some { get; }
    T dtor_value { get; }
    _IOption<__T> DowncastClone<__T>(Func<T, __T> converter0);
    bool IsFailure();
    Std.Wrappers._IOption<__U> PropagateFailure<__U>();
    T Extract();
    Std.Wrappers._IResult<T, __E> ToResult<__E>(__E error);
    Std.Wrappers._IOutcome<__E> ToOutcome<__E>(__E error);
  }
  public abstract class Option<T> : _IOption<T> {
    public Option() {
    }
    public static Std.Wrappers._IOption<T> Default() {
      return create_None();
    }
    public static Dafny.TypeDescriptor<Std.Wrappers._IOption<T>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.Wrappers._IOption<T>>(Std.Wrappers.Option<T>.Default());
    }
    public static _IOption<T> create_None() {
      return new Option_None<T>();
    }
    public static _IOption<T> create_Some(T @value) {
      return new Option_Some<T>(@value);
    }
    public bool is_None { get { return this is Option_None<T>; } }
    public bool is_Some { get { return this is Option_Some<T>; } }
    public T dtor_value {
      get {
        var d = this;
        return ((Option_Some<T>)d)._value;
      }
    }
    public abstract _IOption<__T> DowncastClone<__T>(Func<T, __T> converter0);
    public bool IsFailure() {
      return (this).is_None;
    }
    public Std.Wrappers._IOption<__U> PropagateFailure<__U>() {
      return Std.Wrappers.Option<__U>.create_None();
    }
    public T Extract() {
      return (this).dtor_value;
    }
    public static T GetOr(Std.Wrappers._IOption<T> _this, T @default) {
      Std.Wrappers._IOption<T> _source0 = _this;
      {
        if (_source0.is_Some) {
          T _0_v = _source0.dtor_value;
          return _0_v;
        }
      }
      {
        return @default;
      }
    }
    public Std.Wrappers._IResult<T, __E> ToResult<__E>(__E error) {
      Std.Wrappers._IOption<T> _source0 = this;
      {
        if (_source0.is_Some) {
          T _0_v = _source0.dtor_value;
          return Std.Wrappers.Result<T, __E>.create_Success(_0_v);
        }
      }
      {
        return Std.Wrappers.Result<T, __E>.create_Failure(error);
      }
    }
    public Std.Wrappers._IOutcome<__E> ToOutcome<__E>(__E error) {
      Std.Wrappers._IOption<T> _source0 = this;
      {
        if (_source0.is_Some) {
          T _0_v = _source0.dtor_value;
          return Std.Wrappers.Outcome<__E>.create_Pass();
        }
      }
      {
        return Std.Wrappers.Outcome<__E>.create_Fail(error);
      }
    }
    public static __FC Map<__FC>(Std.Wrappers._IOption<T> _this, Func<Std.Wrappers._IOption<T>, __FC> rewrap) {
      return Dafny.Helpers.Id<Func<Std.Wrappers._IOption<T>, __FC>>(rewrap)(_this);
    }
  }
  public class Option_None<T> : Option<T> {
    public Option_None() : base() {
    }
    public override _IOption<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _IOption<__T> dt) { return dt; }
      return new Option_None<__T>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Option_None<T>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Option.None";
      return s;
    }
  }
  public class Option_Some<T> : Option<T> {
    public readonly T _value;
    public Option_Some(T @value) : base() {
      this._value = @value;
    }
    public override _IOption<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _IOption<__T> dt) { return dt; }
      return new Option_Some<__T>(converter0(_value));
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Option_Some<T>;
      return oth != null && object.Equals(this._value, oth._value);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._value));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Option.Some";
      s += "(";
      s += Dafny.Helpers.ToString(this._value);
      s += ")";
      return s;
    }
  }

  public interface _IResult<out R, out E> {
    bool is_Success { get; }
    bool is_Failure { get; }
    R dtor_value { get; }
    E dtor_error { get; }
    _IResult<__R, __E> DowncastClone<__R, __E>(Func<R, __R> converter0, Func<E, __E> converter1);
    bool IsFailure();
    Std.Wrappers._IResult<__U, E> PropagateFailure<__U>();
    R Extract();
    Std.Wrappers._IOption<R> ToOption();
    Std.Wrappers._IOutcome<E> ToOutcome();
  }
  public abstract class Result<R, E> : _IResult<R, E> {
    public Result() {
    }
    public static Std.Wrappers._IResult<R, E> Default(R _default_R) {
      return create_Success(_default_R);
    }
    public static Dafny.TypeDescriptor<Std.Wrappers._IResult<R, E>> _TypeDescriptor(Dafny.TypeDescriptor<R> _td_R) {
      return new Dafny.TypeDescriptor<Std.Wrappers._IResult<R, E>>(Std.Wrappers.Result<R, E>.Default(_td_R.Default()));
    }
    public static _IResult<R, E> create_Success(R @value) {
      return new Result_Success<R, E>(@value);
    }
    public static _IResult<R, E> create_Failure(E error) {
      return new Result_Failure<R, E>(error);
    }
    public bool is_Success { get { return this is Result_Success<R, E>; } }
    public bool is_Failure { get { return this is Result_Failure<R, E>; } }
    public R dtor_value {
      get {
        var d = this;
        return ((Result_Success<R, E>)d)._value;
      }
    }
    public E dtor_error {
      get {
        var d = this;
        return ((Result_Failure<R, E>)d)._error;
      }
    }
    public abstract _IResult<__R, __E> DowncastClone<__R, __E>(Func<R, __R> converter0, Func<E, __E> converter1);
    public bool IsFailure() {
      return (this).is_Failure;
    }
    public Std.Wrappers._IResult<__U, E> PropagateFailure<__U>() {
      return Std.Wrappers.Result<__U, E>.create_Failure((this).dtor_error);
    }
    public R Extract() {
      return (this).dtor_value;
    }
    public static R GetOr(Std.Wrappers._IResult<R, E> _this, R @default) {
      Std.Wrappers._IResult<R, E> _source0 = _this;
      {
        if (_source0.is_Success) {
          R _0_s = _source0.dtor_value;
          return _0_s;
        }
      }
      {
        E _1_e = _source0.dtor_error;
        return @default;
      }
    }
    public Std.Wrappers._IOption<R> ToOption() {
      Std.Wrappers._IResult<R, E> _source0 = this;
      {
        if (_source0.is_Success) {
          R _0_s = _source0.dtor_value;
          return Std.Wrappers.Option<R>.create_Some(_0_s);
        }
      }
      {
        E _1_e = _source0.dtor_error;
        return Std.Wrappers.Option<R>.create_None();
      }
    }
    public Std.Wrappers._IOutcome<E> ToOutcome() {
      Std.Wrappers._IResult<R, E> _source0 = this;
      {
        if (_source0.is_Success) {
          R _0_s = _source0.dtor_value;
          return Std.Wrappers.Outcome<E>.create_Pass();
        }
      }
      {
        E _1_e = _source0.dtor_error;
        return Std.Wrappers.Outcome<E>.create_Fail(_1_e);
      }
    }
    public static __FC Map<__FC>(Std.Wrappers._IResult<R, E> _this, Func<Std.Wrappers._IResult<R, E>, __FC> rewrap) {
      return Dafny.Helpers.Id<Func<Std.Wrappers._IResult<R, E>, __FC>>(rewrap)(_this);
    }
    public static Std.Wrappers._IResult<R, __NewE> MapFailure<__NewE>(Std.Wrappers._IResult<R, E> _this, Func<E, __NewE> reWrap) {
      Std.Wrappers._IResult<R, E> _source0 = _this;
      {
        if (_source0.is_Success) {
          R _0_s = _source0.dtor_value;
          return Std.Wrappers.Result<R, __NewE>.create_Success(_0_s);
        }
      }
      {
        E _1_e = _source0.dtor_error;
        return Std.Wrappers.Result<R, __NewE>.create_Failure(Dafny.Helpers.Id<Func<E, __NewE>>(reWrap)(_1_e));
      }
    }
  }
  public class Result_Success<R, E> : Result<R, E> {
    public readonly R _value;
    public Result_Success(R @value) : base() {
      this._value = @value;
    }
    public override _IResult<__R, __E> DowncastClone<__R, __E>(Func<R, __R> converter0, Func<E, __E> converter1) {
      if (this is _IResult<__R, __E> dt) { return dt; }
      return new Result_Success<__R, __E>(converter0(_value));
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Result_Success<R, E>;
      return oth != null && object.Equals(this._value, oth._value);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._value));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Result.Success";
      s += "(";
      s += Dafny.Helpers.ToString(this._value);
      s += ")";
      return s;
    }
  }
  public class Result_Failure<R, E> : Result<R, E> {
    public readonly E _error;
    public Result_Failure(E error) : base() {
      this._error = error;
    }
    public override _IResult<__R, __E> DowncastClone<__R, __E>(Func<R, __R> converter0, Func<E, __E> converter1) {
      if (this is _IResult<__R, __E> dt) { return dt; }
      return new Result_Failure<__R, __E>(converter1(_error));
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Result_Failure<R, E>;
      return oth != null && object.Equals(this._error, oth._error);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._error));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Result.Failure";
      s += "(";
      s += Dafny.Helpers.ToString(this._error);
      s += ")";
      return s;
    }
  }

  public interface _IOutcome<out E> {
    bool is_Pass { get; }
    bool is_Fail { get; }
    E dtor_error { get; }
    _IOutcome<__E> DowncastClone<__E>(Func<E, __E> converter0);
    bool IsFailure();
    Std.Wrappers._IOutcome<E> PropagateFailure();
    Std.Wrappers._IOption<__R> ToOption<__R>(__R r);
    Std.Wrappers._IResult<__R, E> ToResult<__R>(__R r);
  }
  public abstract class Outcome<E> : _IOutcome<E> {
    public Outcome() {
    }
    public static Std.Wrappers._IOutcome<E> Default() {
      return create_Pass();
    }
    public static Dafny.TypeDescriptor<Std.Wrappers._IOutcome<E>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.Wrappers._IOutcome<E>>(Std.Wrappers.Outcome<E>.Default());
    }
    public static _IOutcome<E> create_Pass() {
      return new Outcome_Pass<E>();
    }
    public static _IOutcome<E> create_Fail(E error) {
      return new Outcome_Fail<E>(error);
    }
    public bool is_Pass { get { return this is Outcome_Pass<E>; } }
    public bool is_Fail { get { return this is Outcome_Fail<E>; } }
    public E dtor_error {
      get {
        var d = this;
        return ((Outcome_Fail<E>)d)._error;
      }
    }
    public abstract _IOutcome<__E> DowncastClone<__E>(Func<E, __E> converter0);
    public bool IsFailure() {
      return (this).is_Fail;
    }
    public Std.Wrappers._IOutcome<E> PropagateFailure() {
      return this;
    }
    public Std.Wrappers._IOption<__R> ToOption<__R>(__R r) {
      Std.Wrappers._IOutcome<E> _source0 = this;
      {
        if (_source0.is_Pass) {
          return Std.Wrappers.Option<__R>.create_Some(r);
        }
      }
      {
        E _0_e = _source0.dtor_error;
        return Std.Wrappers.Option<__R>.create_None();
      }
    }
    public Std.Wrappers._IResult<__R, E> ToResult<__R>(__R r) {
      Std.Wrappers._IOutcome<E> _source0 = this;
      {
        if (_source0.is_Pass) {
          return Std.Wrappers.Result<__R, E>.create_Success(r);
        }
      }
      {
        E _0_e = _source0.dtor_error;
        return Std.Wrappers.Result<__R, E>.create_Failure(_0_e);
      }
    }
    public static __FC Map<__FC>(Std.Wrappers._IOutcome<E> _this, Func<Std.Wrappers._IOutcome<E>, __FC> rewrap) {
      return Dafny.Helpers.Id<Func<Std.Wrappers._IOutcome<E>, __FC>>(rewrap)(_this);
    }
    public static Std.Wrappers._IResult<__T, __NewE> MapFailure<__T, __NewE>(Std.Wrappers._IOutcome<E> _this, Func<E, __NewE> rewrap, __T @default)
    {
      Std.Wrappers._IOutcome<E> _source0 = _this;
      {
        if (_source0.is_Pass) {
          return Std.Wrappers.Result<__T, __NewE>.create_Success(@default);
        }
      }
      {
        E _0_e = _source0.dtor_error;
        return Std.Wrappers.Result<__T, __NewE>.create_Failure(Dafny.Helpers.Id<Func<E, __NewE>>(rewrap)(_0_e));
      }
    }
    public static Std.Wrappers._IOutcome<E> Need(bool condition, E error)
    {
      if (condition) {
        return Std.Wrappers.Outcome<E>.create_Pass();
      } else {
        return Std.Wrappers.Outcome<E>.create_Fail(error);
      }
    }
  }
  public class Outcome_Pass<E> : Outcome<E> {
    public Outcome_Pass() : base() {
    }
    public override _IOutcome<__E> DowncastClone<__E>(Func<E, __E> converter0) {
      if (this is _IOutcome<__E> dt) { return dt; }
      return new Outcome_Pass<__E>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Outcome_Pass<E>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Outcome.Pass";
      return s;
    }
  }
  public class Outcome_Fail<E> : Outcome<E> {
    public readonly E _error;
    public Outcome_Fail(E error) : base() {
      this._error = error;
    }
    public override _IOutcome<__E> DowncastClone<__E>(Func<E, __E> converter0) {
      if (this is _IOutcome<__E> dt) { return dt; }
      return new Outcome_Fail<__E>(converter0(_error));
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.Outcome_Fail<E>;
      return oth != null && object.Equals(this._error, oth._error);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._error));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.Outcome.Fail";
      s += "(";
      s += Dafny.Helpers.ToString(this._error);
      s += ")";
      return s;
    }
  }

  public interface _IOutcomeResult<out E> {
    bool is_Pass_k { get; }
    bool is_Fail_k { get; }
    E dtor_error { get; }
    _IOutcomeResult<__E> DowncastClone<__E>(Func<E, __E> converter0);
    bool IsFailure();
    Std.Wrappers._IResult<__U, E> PropagateFailure<__U>();
  }
  public abstract class OutcomeResult<E> : _IOutcomeResult<E> {
    public OutcomeResult() {
    }
    public static Std.Wrappers._IOutcomeResult<E> Default() {
      return create_Pass_k();
    }
    public static Dafny.TypeDescriptor<Std.Wrappers._IOutcomeResult<E>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.Wrappers._IOutcomeResult<E>>(Std.Wrappers.OutcomeResult<E>.Default());
    }
    public static _IOutcomeResult<E> create_Pass_k() {
      return new OutcomeResult_Pass_k<E>();
    }
    public static _IOutcomeResult<E> create_Fail_k(E error) {
      return new OutcomeResult_Fail_k<E>(error);
    }
    public bool is_Pass_k { get { return this is OutcomeResult_Pass_k<E>; } }
    public bool is_Fail_k { get { return this is OutcomeResult_Fail_k<E>; } }
    public E dtor_error {
      get {
        var d = this;
        return ((OutcomeResult_Fail_k<E>)d)._error;
      }
    }
    public abstract _IOutcomeResult<__E> DowncastClone<__E>(Func<E, __E> converter0);
    public bool IsFailure() {
      return (this).is_Fail_k;
    }
    public Std.Wrappers._IResult<__U, E> PropagateFailure<__U>() {
      return Std.Wrappers.Result<__U, E>.create_Failure((this).dtor_error);
    }
  }
  public class OutcomeResult_Pass_k<E> : OutcomeResult<E> {
    public OutcomeResult_Pass_k() : base() {
    }
    public override _IOutcomeResult<__E> DowncastClone<__E>(Func<E, __E> converter0) {
      if (this is _IOutcomeResult<__E> dt) { return dt; }
      return new OutcomeResult_Pass_k<__E>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.OutcomeResult_Pass_k<E>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.OutcomeResult.Pass'";
      return s;
    }
  }
  public class OutcomeResult_Fail_k<E> : OutcomeResult<E> {
    public readonly E _error;
    public OutcomeResult_Fail_k(E error) : base() {
      this._error = error;
    }
    public override _IOutcomeResult<__E> DowncastClone<__E>(Func<E, __E> converter0) {
      if (this is _IOutcomeResult<__E> dt) { return dt; }
      return new OutcomeResult_Fail_k<__E>(converter0(_error));
    }
    public override bool Equals(object other) {
      var oth = other as Std.Wrappers.OutcomeResult_Fail_k<E>;
      return oth != null && object.Equals(this._error, oth._error);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._error));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Wrappers.OutcomeResult.Fail'";
      s += "(";
      s += Dafny.Helpers.ToString(this._error);
      s += ")";
      return s;
    }
  }
} // end of namespace Std.Wrappers
namespace Std.FileIOInternalExterns {

} // end of namespace Std.FileIOInternalExterns
namespace Std.FileIO {

  public partial class __default {
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>> ReadBytesFromFile(Dafny.ISequence<Dafny.Rune> path)
    {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>> res = Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.Default(Dafny.Sequence<byte>.Empty);
      bool _0_isError;
      Dafny.ISequence<byte> _1_bytesRead;
      Dafny.ISequence<Dafny.Rune> _2_errorMsg;
      bool _out0;
      Dafny.ISequence<byte> _out1;
      Dafny.ISequence<Dafny.Rune> _out2;
      Std.FileIOInternalExterns.__default.INTERNAL__ReadBytesFromFile(path, out _out0, out _out1, out _out2);
      _0_isError = _out0;
      _1_bytesRead = _out1;
      _2_errorMsg = _out2;
      if (_0_isError) {
        res = Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Failure(_2_errorMsg);
      } else {
        res = Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Success(_1_bytesRead);
      }
      return res;
      return res;
    }
    public static Std.Wrappers._IResult<_System._ITuple0, Dafny.ISequence<Dafny.Rune>> WriteBytesToFile(Dafny.ISequence<Dafny.Rune> path, Dafny.ISequence<byte> bytes)
    {
      Std.Wrappers._IResult<_System._ITuple0, Dafny.ISequence<Dafny.Rune>> res = Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.Default(_System.Tuple0.Default());
      bool _0_isError;
      Dafny.ISequence<Dafny.Rune> _1_errorMsg;
      bool _out0;
      Dafny.ISequence<Dafny.Rune> _out1;
      Std.FileIOInternalExterns.__default.INTERNAL__WriteBytesToFile(path, bytes, out _out0, out _out1);
      _0_isError = _out0;
      _1_errorMsg = _out1;
      if (_0_isError) {
        res = Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Failure(_1_errorMsg);
      } else {
        res = Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Success(_System.Tuple0.create());
      }
      return res;
      return res;
    }
  }
} // end of namespace Std.FileIO
namespace Std.BoundedInts {

  public partial class __default {
    public static BigInteger TWO__TO__THE__8 { get {
      return new BigInteger(256);
    } }
    public static byte UINT8__MAX { get {
      return (byte)(255);
    } }
    public static BigInteger TWO__TO__THE__16 { get {
      return new BigInteger(65536);
    } }
    public static ushort UINT16__MAX { get {
      return (ushort)(65535);
    } }
    public static BigInteger TWO__TO__THE__32 { get {
      return new BigInteger(4294967296L);
    } }
    public static uint UINT32__MAX { get {
      return 4294967295U;
    } }
    public static BigInteger TWO__TO__THE__64 { get {
      return BigInteger.Parse("18446744073709551616");
    } }
    public static ulong UINT64__MAX { get {
      return 18446744073709551615UL;
    } }
    public static BigInteger TWO__TO__THE__7 { get {
      return new BigInteger(128);
    } }
    public static sbyte INT8__MIN { get {
      return (sbyte)(-128);
    } }
    public static sbyte INT8__MAX { get {
      return (sbyte)(127);
    } }
    public static BigInteger TWO__TO__THE__15 { get {
      return new BigInteger(32768);
    } }
    public static short INT16__MIN { get {
      return (short)(-32768);
    } }
    public static short INT16__MAX { get {
      return (short)(32767);
    } }
    public static BigInteger TWO__TO__THE__31 { get {
      return new BigInteger(2147483648L);
    } }
    public static int INT32__MIN { get {
      return -2147483648;
    } }
    public static int INT32__MAX { get {
      return 2147483647;
    } }
    public static BigInteger TWO__TO__THE__63 { get {
      return new BigInteger(9223372036854775808UL);
    } }
    public static long INT64__MIN { get {
      return -9223372036854775808L;
    } }
    public static long INT64__MAX { get {
      return 9223372036854775807L;
    } }
    public static byte NAT8__MAX { get {
      return (byte)(127);
    } }
    public static ushort NAT16__MAX { get {
      return (ushort)(32767);
    } }
    public static uint NAT32__MAX { get {
      return 2147483647U;
    } }
    public static ulong NAT64__MAX { get {
      return 9223372036854775807UL;
    } }
    public static BigInteger TWO__TO__THE__128 { get {
      return BigInteger.Parse("340282366920938463463374607431768211456");
    } }
    public static BigInteger TWO__TO__THE__127 { get {
      return BigInteger.Parse("170141183460469231731687303715884105728");
    } }
    public static BigInteger TWO__TO__THE__0 { get {
      return BigInteger.One;
    } }
    public static BigInteger TWO__TO__THE__1 { get {
      return new BigInteger(2);
    } }
    public static BigInteger TWO__TO__THE__2 { get {
      return new BigInteger(4);
    } }
    public static BigInteger TWO__TO__THE__4 { get {
      return new BigInteger(16);
    } }
    public static BigInteger TWO__TO__THE__5 { get {
      return new BigInteger(32);
    } }
    public static BigInteger TWO__TO__THE__24 { get {
      return new BigInteger(16777216);
    } }
    public static BigInteger TWO__TO__THE__40 { get {
      return new BigInteger(1099511627776L);
    } }
    public static BigInteger TWO__TO__THE__48 { get {
      return new BigInteger(281474976710656L);
    } }
    public static BigInteger TWO__TO__THE__56 { get {
      return new BigInteger(72057594037927936L);
    } }
    public static BigInteger TWO__TO__THE__256 { get {
      return BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639936");
    } }
    public static BigInteger TWO__TO__THE__512 { get {
      return BigInteger.Parse("13407807929942597099574024998205846127479365820592393377723561443721764030073546976801874298166903427690031858186486050853753882811946569946433649006084096");
    } }
  }

  public partial class uint8 {
    public static System.Collections.Generic.IEnumerable<byte> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (byte)j; }
    }
    private static readonly Dafny.TypeDescriptor<byte> _TYPE = new Dafny.TypeDescriptor<byte>(0);
    public static Dafny.TypeDescriptor<byte> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(byte __source) {
      return true;
    }
  }

  public partial class uint16 {
    public static System.Collections.Generic.IEnumerable<ushort> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (ushort)j; }
    }
    private static readonly Dafny.TypeDescriptor<ushort> _TYPE = new Dafny.TypeDescriptor<ushort>(0);
    public static Dafny.TypeDescriptor<ushort> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(ushort __source) {
      return true;
    }
  }

  public partial class uint32 {
    public static System.Collections.Generic.IEnumerable<uint> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (uint)j; }
    }
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(0);
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      return true;
    }
  }

  public partial class uint64 {
    public static System.Collections.Generic.IEnumerable<ulong> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (ulong)j; }
    }
    private static readonly Dafny.TypeDescriptor<ulong> _TYPE = new Dafny.TypeDescriptor<ulong>(0);
    public static Dafny.TypeDescriptor<ulong> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(ulong __source) {
      return true;
    }
  }

  public partial class uint128 {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _0_x = __source;
      return ((_0_x).Sign != -1) && ((_0_x) < (Std.BoundedInts.__default.TWO__TO__THE__128));
    }
  }

  public partial class int8 {
    public static System.Collections.Generic.IEnumerable<sbyte> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (sbyte)j; }
    }
    private static readonly Dafny.TypeDescriptor<sbyte> _TYPE = new Dafny.TypeDescriptor<sbyte>(0);
    public static Dafny.TypeDescriptor<sbyte> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(sbyte __source) {
      return true;
    }
  }

  public partial class int16 {
    public static System.Collections.Generic.IEnumerable<short> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (short)j; }
    }
    private static readonly Dafny.TypeDescriptor<short> _TYPE = new Dafny.TypeDescriptor<short>(0);
    public static Dafny.TypeDescriptor<short> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(short __source) {
      return true;
    }
  }

  public partial class int32 {
    public static System.Collections.Generic.IEnumerable<int> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (int)j; }
    }
    private static readonly Dafny.TypeDescriptor<int> _TYPE = new Dafny.TypeDescriptor<int>(0);
    public static Dafny.TypeDescriptor<int> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(int __source) {
      return true;
    }
  }

  public partial class int64 {
    public static System.Collections.Generic.IEnumerable<long> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (long)j; }
    }
    private static readonly Dafny.TypeDescriptor<long> _TYPE = new Dafny.TypeDescriptor<long>(0);
    public static Dafny.TypeDescriptor<long> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(long __source) {
      return true;
    }
  }

  public partial class int128 {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _1_x = __source;
      return (((BigInteger.Zero) - (Std.BoundedInts.__default.TWO__TO__THE__127)) <= (_1_x)) && ((_1_x) < (Std.BoundedInts.__default.TWO__TO__THE__127));
    }
  }

  public partial class nat8 {
    public static System.Collections.Generic.IEnumerable<byte> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (byte)j; }
    }
    private static readonly Dafny.TypeDescriptor<byte> _TYPE = new Dafny.TypeDescriptor<byte>(0);
    public static Dafny.TypeDescriptor<byte> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(byte __source) {
      BigInteger _2_x = new BigInteger(__source);
      return ((_2_x).Sign != -1) && ((_2_x) < (Std.BoundedInts.__default.TWO__TO__THE__7));
    }
  }

  public partial class nat16 {
    public static System.Collections.Generic.IEnumerable<ushort> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (ushort)j; }
    }
    private static readonly Dafny.TypeDescriptor<ushort> _TYPE = new Dafny.TypeDescriptor<ushort>(0);
    public static Dafny.TypeDescriptor<ushort> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(ushort __source) {
      BigInteger _3_x = new BigInteger(__source);
      return ((_3_x).Sign != -1) && ((_3_x) < (Std.BoundedInts.__default.TWO__TO__THE__15));
    }
  }

  public partial class nat32 {
    public static System.Collections.Generic.IEnumerable<uint> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (uint)j; }
    }
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(0);
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      BigInteger _4_x = new BigInteger(__source);
      return ((_4_x).Sign != -1) && ((_4_x) < (Std.BoundedInts.__default.TWO__TO__THE__31));
    }
  }

  public partial class nat64 {
    public static System.Collections.Generic.IEnumerable<ulong> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (ulong)j; }
    }
    private static readonly Dafny.TypeDescriptor<ulong> _TYPE = new Dafny.TypeDescriptor<ulong>(0);
    public static Dafny.TypeDescriptor<ulong> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(ulong __source) {
      BigInteger _5_x = new BigInteger(__source);
      return ((_5_x).Sign != -1) && ((_5_x) < (Std.BoundedInts.__default.TWO__TO__THE__63));
    }
  }

  public partial class nat128 {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _6_x = __source;
      return ((_6_x).Sign != -1) && ((_6_x) < (Std.BoundedInts.__default.TWO__TO__THE__127));
    }
  }

  public partial class opt__byte {
    public static System.Collections.Generic.IEnumerable<short> IntegerRange(BigInteger lo, BigInteger hi) {
      for (var j = lo; j < hi; j++) { yield return (short)j; }
    }
    private static readonly Dafny.TypeDescriptor<short> _TYPE = new Dafny.TypeDescriptor<short>(0);
    public static Dafny.TypeDescriptor<short> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(short __source) {
      BigInteger _7_c = new BigInteger(__source);
      return ((new BigInteger(-1)) <= (_7_c)) && ((_7_c) < (Std.BoundedInts.__default.TWO__TO__THE__8));
    }
  }
} // end of namespace Std.BoundedInts
namespace Std.Base64 {

  public partial class __default {
    public static bool IsBase64Char(Dafny.Rune c) {
      return (((((c) == (new Dafny.Rune('+'))) || ((c) == (new Dafny.Rune('/')))) || (((new Dafny.Rune('0')) <= (c)) && ((c) <= (new Dafny.Rune('9'))))) || (((new Dafny.Rune('A')) <= (c)) && ((c) <= (new Dafny.Rune('Z'))))) || (((new Dafny.Rune('a')) <= (c)) && ((c) <= (new Dafny.Rune('z'))));
    }
    public static bool IsUnpaddedBase64String(Dafny.ISequence<Dafny.Rune> s) {
      return ((Dafny.Helpers.EuclideanModulus(new BigInteger((s).Count), new BigInteger(4))).Sign == 0) && (Dafny.Helpers.Id<Func<Dafny.ISequence<Dafny.Rune>, bool>>((_0_s) => Dafny.Helpers.Quantifier<Dafny.Rune>((_0_s).UniqueElements, true, (((_forall_var_0) => {
        Dafny.Rune _1_k = (Dafny.Rune)_forall_var_0;
        return !((_0_s).Contains(_1_k)) || (Std.Base64.__default.IsBase64Char(_1_k));
      }))))(s));
    }
    public static Dafny.Rune IndexToChar(byte i) {
      if ((i) == ((byte)(63))) {
        return new Dafny.Rune('/');
      } else if ((i) == ((byte)(62))) {
        return new Dafny.Rune('+');
      } else if ((((byte)(52)) <= (i)) && ((i) <= ((byte)(61)))) {
        return new Dafny.Rune((int)(new BigInteger(unchecked((byte)(((byte)((i) - ((byte)(4)))) & (byte)0x3F)))));
      } else if ((((byte)(26)) <= (i)) && ((i) <= ((byte)(51)))) {
        return Dafny.Helpers.AddRunes(new Dafny.Rune((int)(new BigInteger(i))), new Dafny.Rune((int)(new BigInteger(71))));
      } else {
        return Dafny.Helpers.AddRunes(new Dafny.Rune((int)(new BigInteger(i))), new Dafny.Rune((int)(new BigInteger(65))));
      }
    }
    public static byte CharToIndex(Dafny.Rune c) {
      if ((c) == (new Dafny.Rune('/'))) {
        return (byte)(63);
      } else if ((c) == (new Dafny.Rune('+'))) {
        return (byte)(62);
      } else if (((new Dafny.Rune('0')) <= (c)) && ((c) <= (new Dafny.Rune('9')))) {
        return (byte)(new BigInteger((Dafny.Helpers.AddRunes(c, new Dafny.Rune((int)(new BigInteger(4))))).Value));
      } else if (((new Dafny.Rune('a')) <= (c)) && ((c) <= (new Dafny.Rune('z')))) {
        return (byte)(new BigInteger((Dafny.Helpers.SubtractRunes(c, new Dafny.Rune((int)(new BigInteger(71))))).Value));
      } else {
        return (byte)(new BigInteger((Dafny.Helpers.SubtractRunes(c, new Dafny.Rune((int)(new BigInteger(65))))).Value));
      }
    }
    public static Dafny.ISequence<byte> BV24ToSeq(uint x) {
      byte _0_b0 = (byte)(((x) >> ((int)((byte)(16)))) & (255U));
      byte _1_b1 = (byte)(((x) >> ((int)((byte)(8)))) & (255U));
      byte _2_b2 = (byte)((x) & (255U));
      return Dafny.Sequence<byte>.FromElements(_0_b0, _1_b1, _2_b2);
    }
    public static uint SeqToBV24(Dafny.ISequence<byte> x) {
      return ((unchecked((uint)((((uint)((x).Select(BigInteger.Zero))) << ((int)((byte)(16)))) & (uint)0xFFFFFFU))) | (unchecked((uint)((((uint)((x).Select(BigInteger.One))) << ((int)((byte)(8)))) & (uint)0xFFFFFFU)))) | ((uint)((x).Select(new BigInteger(2))));
    }
    public static Dafny.ISequence<byte> BV24ToIndexSeq(uint x) {
      byte _0_b0 = (byte)(((x) >> ((int)((byte)(18)))) & (63U));
      byte _1_b1 = (byte)(((x) >> ((int)((byte)(12)))) & (63U));
      byte _2_b2 = (byte)(((x) >> ((int)((byte)(6)))) & (63U));
      byte _3_b3 = (byte)((x) & (63U));
      return Dafny.Sequence<byte>.FromElements(_0_b0, _1_b1, _2_b2, _3_b3);
    }
    public static uint IndexSeqToBV24(Dafny.ISequence<byte> x) {
      return (((unchecked((uint)((((uint)((x).Select(BigInteger.Zero))) << ((int)((byte)(18)))) & (uint)0xFFFFFFU))) | (unchecked((uint)((((uint)((x).Select(BigInteger.One))) << ((int)((byte)(12)))) & (uint)0xFFFFFFU)))) | (unchecked((uint)((((uint)((x).Select(new BigInteger(2)))) << ((int)((byte)(6)))) & (uint)0xFFFFFFU)))) | ((uint)((x).Select(new BigInteger(3))));
    }
    public static Dafny.ISequence<byte> DecodeBlock(Dafny.ISequence<byte> s) {
      return Std.Base64.__default.BV24ToSeq(Std.Base64.__default.IndexSeqToBV24(s));
    }
    public static Dafny.ISequence<byte> EncodeBlock(Dafny.ISequence<byte> s) {
      return Std.Base64.__default.BV24ToIndexSeq(Std.Base64.__default.SeqToBV24(s));
    }
    public static Dafny.ISequence<byte> DecodeRecursively(Dafny.ISequence<byte> s)
    {
      Dafny.ISequence<byte> b = Dafny.Sequence<byte>.Empty;
      BigInteger _0_resultLength;
      _0_resultLength = (Dafny.Helpers.EuclideanDivision(new BigInteger((s).Count), new BigInteger(4))) * (new BigInteger(3));
      byte[] _1_result;
      Func<BigInteger, byte> _init0 = ((System.Func<BigInteger, byte>)((_2_i) => {
        return (byte)(0);
      }));
      byte[] _nw0 = new byte[Dafny.Helpers.ToIntChecked(_0_resultLength, "array size exceeds memory limit")];
      for (var _i0_0 = 0; _i0_0 < new BigInteger(_nw0.Length); _i0_0++) {
        _nw0[(int)(_i0_0)] = _init0(_i0_0);
      }
      _1_result = _nw0;
      BigInteger _3_i;
      _3_i = new BigInteger((s).Count);
      BigInteger _4_j;
      _4_j = _0_resultLength;
      while ((_3_i).Sign == 1) {
        _3_i = (_3_i) - (new BigInteger(4));
        _4_j = (_4_j) - (new BigInteger(3));
        Dafny.ISequence<byte> _5_block;
        _5_block = Std.Base64.__default.DecodeBlock((s).Subsequence(_3_i, (_3_i) + (new BigInteger(4))));
        (_1_result)[(int)((_4_j))] = (_5_block).Select(BigInteger.Zero);
        BigInteger _index0 = (_4_j) + (BigInteger.One);
        (_1_result)[(int)(_index0)] = (_5_block).Select(BigInteger.One);
        BigInteger _index1 = (_4_j) + (new BigInteger(2));
        (_1_result)[(int)(_index1)] = (_5_block).Select(new BigInteger(2));
      }
      b = Dafny.Helpers.SeqFromArray(_1_result);
      return b;
    }
    public static Dafny.ISequence<byte> EncodeRecursively(Dafny.ISequence<byte> b)
    {
      Dafny.ISequence<byte> s = Dafny.Sequence<byte>.Empty;
      BigInteger _0_resultLength;
      _0_resultLength = (Dafny.Helpers.EuclideanDivision(new BigInteger((b).Count), new BigInteger(3))) * (new BigInteger(4));
      byte[] _1_result;
      Func<BigInteger, byte> _init0 = ((System.Func<BigInteger, byte>)((_2_i) => {
        return (byte)(0);
      }));
      byte[] _nw0 = new byte[Dafny.Helpers.ToIntChecked(_0_resultLength, "array size exceeds memory limit")];
      for (var _i0_0 = 0; _i0_0 < new BigInteger(_nw0.Length); _i0_0++) {
        _nw0[(int)(_i0_0)] = _init0(_i0_0);
      }
      _1_result = _nw0;
      BigInteger _3_i;
      _3_i = new BigInteger((b).Count);
      BigInteger _4_j;
      _4_j = _0_resultLength;
      while ((_3_i).Sign == 1) {
        _3_i = (_3_i) - (new BigInteger(3));
        _4_j = (_4_j) - (new BigInteger(4));
        Dafny.ISequence<byte> _5_block;
        _5_block = Std.Base64.__default.EncodeBlock((b).Subsequence(_3_i, (_3_i) + (new BigInteger(3))));
        (_1_result)[(int)((_4_j))] = (_5_block).Select(BigInteger.Zero);
        BigInteger _index0 = (_4_j) + (BigInteger.One);
        (_1_result)[(int)(_index0)] = (_5_block).Select(BigInteger.One);
        BigInteger _index1 = (_4_j) + (new BigInteger(2));
        (_1_result)[(int)(_index1)] = (_5_block).Select(new BigInteger(2));
        BigInteger _index2 = (_4_j) + (new BigInteger(3));
        (_1_result)[(int)(_index2)] = (_5_block).Select(new BigInteger(3));
      }
      s = Dafny.Helpers.SeqFromArray(_1_result);
      return s;
    }
    public static Dafny.ISequence<byte> FromCharsToIndices(Dafny.ISequence<Dafny.Rune> s) {
      return ((System.Func<Dafny.ISequence<byte>>) (() => {
        BigInteger dim0 = new BigInteger((s).Count);
        var arr0 = new byte[Dafny.Helpers.ToIntChecked(dim0, "array size exceeds memory limit")];
        for (int i0 = 0; i0 < dim0; i0++) {
          var _0_i = (BigInteger) i0;
          arr0[(int)(_0_i)] = Std.Base64.__default.CharToIndex((s).Select(_0_i));
        }
        return Dafny.Sequence<byte>.FromArray(arr0);
      }))();
    }
    public static Dafny.ISequence<Dafny.Rune> FromIndicesToChars(Dafny.ISequence<byte> b) {
      return ((System.Func<Dafny.ISequence<Dafny.Rune>>) (() => {
        BigInteger dim1 = new BigInteger((b).Count);
        var arr1 = new Dafny.Rune[Dafny.Helpers.ToIntChecked(dim1, "array size exceeds memory limit")];
        for (int i1 = 0; i1 < dim1; i1++) {
          var _0_i = (BigInteger) i1;
          arr1[(int)(_0_i)] = Std.Base64.__default.IndexToChar((b).Select(_0_i));
        }
        return Dafny.Sequence<Dafny.Rune>.FromArray(arr1);
      }))();
    }
    public static Dafny.ISequence<byte> DecodeUnpadded(Dafny.ISequence<Dafny.Rune> s) {
      return Std.Base64.__default.DecodeRecursively(Std.Base64.__default.FromCharsToIndices(s));
    }
    public static Dafny.ISequence<Dafny.Rune> EncodeUnpadded(Dafny.ISequence<byte> b) {
      return Std.Base64.__default.FromIndicesToChars(Std.Base64.__default.EncodeRecursively(b));
    }
    public static bool Is1Padding(Dafny.ISequence<Dafny.Rune> s) {
      return ((((((new BigInteger((s).Count)) == (new BigInteger(4))) && (Std.Base64.__default.IsBase64Char((s).Select(BigInteger.Zero)))) && (Std.Base64.__default.IsBase64Char((s).Select(BigInteger.One)))) && (Std.Base64.__default.IsBase64Char((s).Select(new BigInteger(2))))) && (((byte)((Std.Base64.__default.CharToIndex((s).Select(new BigInteger(2)))) & ((byte)(3)))) == ((byte)(0)))) && (((s).Select(new BigInteger(3))) == (new Dafny.Rune('=')));
    }
    public static Dafny.ISequence<byte> Decode1Padding(Dafny.ISequence<Dafny.Rune> s) {
      Dafny.ISequence<byte> _0_d = Std.Base64.__default.DecodeBlock(Dafny.Sequence<byte>.FromElements(Std.Base64.__default.CharToIndex((s).Select(BigInteger.Zero)), Std.Base64.__default.CharToIndex((s).Select(BigInteger.One)), Std.Base64.__default.CharToIndex((s).Select(new BigInteger(2))), (byte)(0)));
      return Dafny.Sequence<byte>.FromElements((_0_d).Select(BigInteger.Zero), (_0_d).Select(BigInteger.One));
    }
    public static Dafny.ISequence<Dafny.Rune> Encode1Padding(Dafny.ISequence<byte> b) {
      Dafny.ISequence<byte> _0_e = Std.Base64.__default.EncodeBlock(Dafny.Sequence<byte>.FromElements((b).Select(BigInteger.Zero), (b).Select(BigInteger.One), (byte)(0)));
      return Dafny.Sequence<Dafny.Rune>.FromElements(Std.Base64.__default.IndexToChar((_0_e).Select(BigInteger.Zero)), Std.Base64.__default.IndexToChar((_0_e).Select(BigInteger.One)), Std.Base64.__default.IndexToChar((_0_e).Select(new BigInteger(2))), new Dafny.Rune('='));
    }
    public static bool Is2Padding(Dafny.ISequence<Dafny.Rune> s) {
      return ((((((new BigInteger((s).Count)) == (new BigInteger(4))) && (Std.Base64.__default.IsBase64Char((s).Select(BigInteger.Zero)))) && (Std.Base64.__default.IsBase64Char((s).Select(BigInteger.One)))) && (((byte)((Std.Base64.__default.CharToIndex((s).Select(BigInteger.One))) % ((byte)(16)))) == ((byte)(0)))) && (((s).Select(new BigInteger(2))) == (new Dafny.Rune('=')))) && (((s).Select(new BigInteger(3))) == (new Dafny.Rune('=')));
    }
    public static Dafny.ISequence<byte> Decode2Padding(Dafny.ISequence<Dafny.Rune> s) {
      Dafny.ISequence<byte> _0_d = Std.Base64.__default.DecodeBlock(Dafny.Sequence<byte>.FromElements(Std.Base64.__default.CharToIndex((s).Select(BigInteger.Zero)), Std.Base64.__default.CharToIndex((s).Select(BigInteger.One)), (byte)(0), (byte)(0)));
      return Dafny.Sequence<byte>.FromElements((_0_d).Select(BigInteger.Zero));
    }
    public static Dafny.ISequence<Dafny.Rune> Encode2Padding(Dafny.ISequence<byte> b) {
      Dafny.ISequence<byte> _0_e = Std.Base64.__default.EncodeBlock(Dafny.Sequence<byte>.FromElements((b).Select(BigInteger.Zero), (byte)(0), (byte)(0)));
      return Dafny.Sequence<Dafny.Rune>.FromElements(Std.Base64.__default.IndexToChar((_0_e).Select(BigInteger.Zero)), Std.Base64.__default.IndexToChar((_0_e).Select(BigInteger.One)), new Dafny.Rune('='), new Dafny.Rune('='));
    }
    public static bool IsBase64String(Dafny.ISequence<Dafny.Rune> s) {
      BigInteger _0_finalBlockStart = (new BigInteger((s).Count)) - (new BigInteger(4));
      return ((Dafny.Helpers.EuclideanModulus(new BigInteger((s).Count), new BigInteger(4))).Sign == 0) && ((Std.Base64.__default.IsUnpaddedBase64String(s)) || ((Std.Base64.__default.IsUnpaddedBase64String((s).Take(_0_finalBlockStart))) && ((Std.Base64.__default.Is1Padding((s).Drop(_0_finalBlockStart))) || (Std.Base64.__default.Is2Padding((s).Drop(_0_finalBlockStart))))));
    }
    public static Dafny.ISequence<byte> DecodeValid(Dafny.ISequence<Dafny.Rune> s) {
      if ((s).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) {
        return Dafny.Sequence<byte>.FromElements();
      } else {
        BigInteger _0_finalBlockStart = (new BigInteger((s).Count)) - (new BigInteger(4));
        Dafny.ISequence<Dafny.Rune> _1_prefix = (s).Take(_0_finalBlockStart);
        Dafny.ISequence<Dafny.Rune> _2_suffix = (s).Drop(_0_finalBlockStart);
        if (Std.Base64.__default.Is1Padding(_2_suffix)) {
          return Dafny.Sequence<byte>.Concat(Std.Base64.__default.DecodeUnpadded(_1_prefix), Std.Base64.__default.Decode1Padding(_2_suffix));
        } else if (Std.Base64.__default.Is2Padding(_2_suffix)) {
          return Dafny.Sequence<byte>.Concat(Std.Base64.__default.DecodeUnpadded(_1_prefix), Std.Base64.__default.Decode2Padding(_2_suffix));
        } else {
          return Std.Base64.__default.DecodeUnpadded(s);
        }
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>> DecodeBV(Dafny.ISequence<Dafny.Rune> s) {
      if (Std.Base64.__default.IsBase64String(s)) {
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Success(Std.Base64.__default.DecodeValid(s));
      } else {
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Failure(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("The encoding is malformed"));
      }
    }
    public static Dafny.ISequence<Dafny.Rune> EncodeBV(Dafny.ISequence<byte> b) {
      if ((Dafny.Helpers.EuclideanModulus(new BigInteger((b).Count), new BigInteger(3))).Sign == 0) {
        return Std.Base64.__default.EncodeUnpadded(b);
      } else if ((Dafny.Helpers.EuclideanModulus(new BigInteger((b).Count), new BigInteger(3))) == (BigInteger.One)) {
        Dafny.ISequence<Dafny.Rune> _0_s1 = Std.Base64.__default.EncodeUnpadded((b).Take((new BigInteger((b).Count)) - (BigInteger.One)));
        Dafny.ISequence<Dafny.Rune> _1_s2 = Std.Base64.__default.Encode2Padding((b).Drop((new BigInteger((b).Count)) - (BigInteger.One)));
        return Dafny.Sequence<Dafny.Rune>.Concat(_0_s1, _1_s2);
      } else {
        Dafny.ISequence<Dafny.Rune> _2_s1 = Std.Base64.__default.EncodeUnpadded((b).Take((new BigInteger((b).Count)) - (new BigInteger(2))));
        Dafny.ISequence<Dafny.Rune> _3_s2 = Std.Base64.__default.Encode1Padding((b).Drop((new BigInteger((b).Count)) - (new BigInteger(2))));
        return Dafny.Sequence<Dafny.Rune>.Concat(_2_s1, _3_s2);
      }
    }
    public static Dafny.ISequence<byte> UInt8sToBVs(Dafny.ISequence<byte> u) {
      return ((System.Func<Dafny.ISequence<byte>>) (() => {
        BigInteger dim2 = new BigInteger((u).Count);
        var arr2 = new byte[Dafny.Helpers.ToIntChecked(dim2, "array size exceeds memory limit")];
        for (int i2 = 0; i2 < dim2; i2++) {
          var _0_i = (BigInteger) i2;
          arr2[(int)(_0_i)] = (byte)((u).Select(_0_i));
        }
        return Dafny.Sequence<byte>.FromArray(arr2);
      }))();
    }
    public static Dafny.ISequence<byte> BVsToUInt8s(Dafny.ISequence<byte> b) {
      return ((System.Func<Dafny.ISequence<byte>>) (() => {
        BigInteger dim3 = new BigInteger((b).Count);
        var arr3 = new byte[Dafny.Helpers.ToIntChecked(dim3, "array size exceeds memory limit")];
        for (int i3 = 0; i3 < dim3; i3++) {
          var _0_i = (BigInteger) i3;
          arr3[(int)(_0_i)] = (byte)((b).Select(_0_i));
        }
        return Dafny.Sequence<byte>.FromArray(arr3);
      }))();
    }
    public static Dafny.ISequence<Dafny.Rune> Encode(Dafny.ISequence<byte> u) {
      return Std.Base64.__default.EncodeBV(Std.Base64.__default.UInt8sToBVs(u));
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>> Decode(Dafny.ISequence<Dafny.Rune> s) {
      if (Std.Base64.__default.IsBase64String(s)) {
        Dafny.ISequence<byte> _0_b = Std.Base64.__default.DecodeValid(s);
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Success(Std.Base64.__default.BVsToUInt8s(_0_b));
      } else {
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Dafny.ISequence<Dafny.Rune>>.create_Failure(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("The encoding is malformed"));
      }
    }
  }
} // end of namespace Std.Base64
namespace Std.Relations {

} // end of namespace Std.Relations
namespace Std.Math {

  public partial class __default {
    public static BigInteger Min(BigInteger a, BigInteger b)
    {
      if ((a) < (b)) {
        return a;
      } else {
        return b;
      }
    }
    public static BigInteger Min3(BigInteger a, BigInteger b, BigInteger c)
    {
      return Std.Math.__default.Min(a, Std.Math.__default.Min(b, c));
    }
    public static BigInteger Max(BigInteger a, BigInteger b)
    {
      if ((a) < (b)) {
        return b;
      } else {
        return a;
      }
    }
    public static BigInteger Max3(BigInteger a, BigInteger b, BigInteger c)
    {
      return Std.Math.__default.Max(a, Std.Math.__default.Max(b, c));
    }
    public static BigInteger Abs(BigInteger a) {
      if ((a).Sign == -1) {
        return (BigInteger.Zero) - (a);
      } else {
        return a;
      }
    }
  }
} // end of namespace Std.Math
namespace Std.Collections.Seq {

  public partial class __default {
    public static __T First<__T>(Dafny.ISequence<__T> xs) {
      return (xs).Select(BigInteger.Zero);
    }
    public static Dafny.ISequence<__T> DropFirst<__T>(Dafny.ISequence<__T> xs) {
      return (xs).Drop(BigInteger.One);
    }
    public static __T Last<__T>(Dafny.ISequence<__T> xs) {
      return (xs).Select((new BigInteger((xs).Count)) - (BigInteger.One));
    }
    public static Dafny.ISequence<__T> DropLast<__T>(Dafny.ISequence<__T> xs) {
      return (xs).Take((new BigInteger((xs).Count)) - (BigInteger.One));
    }
    public static __T[] ToArray<__T>(Dafny.ISequence<__T> xs)
    {
      __T[] a = new __T[0];
      Func<BigInteger, __T> _init0 = Dafny.Helpers.Id<Func<Dafny.ISequence<__T>, Func<BigInteger, __T>>>((_0_xs) => ((System.Func<BigInteger, __T>)((_1_i) => {
        return (_0_xs).Select(_1_i);
      })))(xs);
      __T[] _nw0 = new __T[Dafny.Helpers.ToIntChecked(new BigInteger((xs).Count), "array size exceeds memory limit")];
      for (var _i0_0 = 0; _i0_0 < new BigInteger(_nw0.Length); _i0_0++) {
        _nw0[(int)(_i0_0)] = _init0(_i0_0);
      }
      a = _nw0;
      return a;
    }
    public static Dafny.ISet<__T> ToSet<__T>(Dafny.ISequence<__T> xs) {
      return Dafny.Helpers.Id<Func<Dafny.ISequence<__T>, Dafny.ISet<__T>>>((_0_xs) => ((System.Func<Dafny.ISet<__T>>)(() => {
        var _coll0 = new System.Collections.Generic.List<__T>();
        foreach (__T _compr_0 in (_0_xs).CloneAsArray()) {
          __T _1_x = (__T)_compr_0;
          if ((_0_xs).Contains(_1_x)) {
            _coll0.Add(_1_x);
          }
        }
        return Dafny.Set<__T>.FromCollection(_coll0);
      }))())(xs);
    }
    public static BigInteger IndexOf<__T>(Dafny.ISequence<__T> xs, __T v)
    {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if (object.Equals((xs).Select(BigInteger.Zero), v)) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (_0___accumulator) + (BigInteger.One);
        Dafny.ISequence<__T> _in0 = (xs).Drop(BigInteger.One);
        __T _in1 = v;
        xs = _in0;
        v = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IOption<BigInteger> IndexOfOption<__T>(Dafny.ISequence<__T> xs, __T v)
    {
      return Std.Collections.Seq.__default.IndexByOption<__T>(xs, Dafny.Helpers.Id<Func<__T, Func<__T, bool>>>((_0_v) => ((System.Func<__T, bool>)((_1_x) => {
        return object.Equals(_1_x, _0_v);
      })))(v));
    }
    public static Std.Wrappers._IOption<BigInteger> IndexByOption<__T>(Dafny.ISequence<__T> xs, Func<__T, bool> p)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Std.Wrappers.Option<BigInteger>.create_None();
      } else if (Dafny.Helpers.Id<Func<__T, bool>>(p)((xs).Select(BigInteger.Zero))) {
        return Std.Wrappers.Option<BigInteger>.create_Some(BigInteger.Zero);
      } else {
        Std.Wrappers._IOption<BigInteger> _0_o_k = Std.Collections.Seq.__default.IndexByOption<__T>((xs).Drop(BigInteger.One), p);
        if ((_0_o_k).is_Some) {
          return Std.Wrappers.Option<BigInteger>.create_Some(((_0_o_k).dtor_value) + (BigInteger.One));
        } else {
          return Std.Wrappers.Option<BigInteger>.create_None();
        }
      }
    }
    public static BigInteger LastIndexOf<__T>(Dafny.ISequence<__T> xs, __T v)
    {
    TAIL_CALL_START: ;
      if (object.Equals((xs).Select((new BigInteger((xs).Count)) - (BigInteger.One)), v)) {
        return (new BigInteger((xs).Count)) - (BigInteger.One);
      } else {
        Dafny.ISequence<__T> _in0 = (xs).Take((new BigInteger((xs).Count)) - (BigInteger.One));
        __T _in1 = v;
        xs = _in0;
        v = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IOption<BigInteger> LastIndexOfOption<__T>(Dafny.ISequence<__T> xs, __T v)
    {
      return Std.Collections.Seq.__default.LastIndexByOption<__T>(xs, Dafny.Helpers.Id<Func<__T, Func<__T, bool>>>((_0_v) => ((System.Func<__T, bool>)((_1_x) => {
        return object.Equals(_1_x, _0_v);
      })))(v));
    }
    public static Std.Wrappers._IOption<BigInteger> LastIndexByOption<__T>(Dafny.ISequence<__T> xs, Func<__T, bool> p)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Std.Wrappers.Option<BigInteger>.create_None();
      } else if (Dafny.Helpers.Id<Func<__T, bool>>(p)((xs).Select((new BigInteger((xs).Count)) - (BigInteger.One)))) {
        return Std.Wrappers.Option<BigInteger>.create_Some((new BigInteger((xs).Count)) - (BigInteger.One));
      } else {
        Dafny.ISequence<__T> _in0 = (xs).Take((new BigInteger((xs).Count)) - (BigInteger.One));
        Func<__T, bool> _in1 = p;
        xs = _in0;
        p = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<__T> Remove<__T>(Dafny.ISequence<__T> xs, BigInteger pos)
    {
      return Dafny.Sequence<__T>.Concat((xs).Take(pos), (xs).Drop((pos) + (BigInteger.One)));
    }
    public static Dafny.ISequence<__T> RemoveValue<__T>(Dafny.ISequence<__T> xs, __T v)
    {
      if (!(xs).Contains(v)) {
        return xs;
      } else {
        BigInteger _0_i = Std.Collections.Seq.__default.IndexOf<__T>(xs, v);
        return Dafny.Sequence<__T>.Concat((xs).Take(_0_i), (xs).Drop((_0_i) + (BigInteger.One)));
      }
    }
    public static Dafny.ISequence<__T> Insert<__T>(Dafny.ISequence<__T> xs, __T a, BigInteger pos)
    {
      return Dafny.Sequence<__T>.Concat(Dafny.Sequence<__T>.Concat((xs).Take(pos), Dafny.Sequence<__T>.FromElements(a)), (xs).Drop(pos));
    }
    public static Dafny.ISequence<__T> Reverse<__T>(Dafny.ISequence<__T> xs) {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((xs).Equals(Dafny.Sequence<__T>.FromElements())) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements((xs).Select((new BigInteger((xs).Count)) - (BigInteger.One))));
        Dafny.ISequence<__T> _in0 = (xs).Subsequence(BigInteger.Zero, (new BigInteger((xs).Count)) - (BigInteger.One));
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<__T> Repeat<__T>(__T v, BigInteger length)
    {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((length).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements(v));
        __T _in0 = v;
        BigInteger _in1 = (length) - (BigInteger.One);
        v = _in0;
        length = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static _System._ITuple2<Dafny.ISequence<__A>, Dafny.ISequence<__B>> Unzip<__A, __B>(Dafny.ISequence<_System._ITuple2<__A, __B>> xs) {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<__A>, Dafny.ISequence<__B>>.create(Dafny.Sequence<__A>.FromElements(), Dafny.Sequence<__B>.FromElements());
      } else {
        _System._ITuple2<Dafny.ISequence<__A>, Dafny.ISequence<__B>> _let_tmp_rhs0 = Std.Collections.Seq.__default.Unzip<__A, __B>(Std.Collections.Seq.__default.DropLast<_System._ITuple2<__A, __B>>(xs));
        Dafny.ISequence<__A> _0_a = _let_tmp_rhs0.dtor__0;
        Dafny.ISequence<__B> _1_b = _let_tmp_rhs0.dtor__1;
        return _System.Tuple2<Dafny.ISequence<__A>, Dafny.ISequence<__B>>.create(Dafny.Sequence<__A>.Concat(_0_a, Dafny.Sequence<__A>.FromElements((Std.Collections.Seq.__default.Last<_System._ITuple2<__A, __B>>(xs)).dtor__0)), Dafny.Sequence<__B>.Concat(_1_b, Dafny.Sequence<__B>.FromElements((Std.Collections.Seq.__default.Last<_System._ITuple2<__A, __B>>(xs)).dtor__1)));
      }
    }
    public static Dafny.ISequence<_System._ITuple2<__A, __B>> Zip<__A, __B>(Dafny.ISequence<__A> xs, Dafny.ISequence<__B> ys)
    {
      Dafny.ISequence<_System._ITuple2<__A, __B>> _0___accumulator = Dafny.Sequence<_System._ITuple2<__A, __B>>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Dafny.Sequence<_System._ITuple2<__A, __B>>.Concat(Dafny.Sequence<_System._ITuple2<__A, __B>>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<_System._ITuple2<__A, __B>>.Concat(Dafny.Sequence<_System._ITuple2<__A, __B>>.FromElements(_System.Tuple2<__A, __B>.create(Std.Collections.Seq.__default.Last<__A>(xs), Std.Collections.Seq.__default.Last<__B>(ys))), _0___accumulator);
        Dafny.ISequence<__A> _in0 = Std.Collections.Seq.__default.DropLast<__A>(xs);
        Dafny.ISequence<__B> _in1 = Std.Collections.Seq.__default.DropLast<__B>(ys);
        xs = _in0;
        ys = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static BigInteger Max(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)) == (BigInteger.One)) {
        return (xs).Select(BigInteger.Zero);
      } else {
        return Std.Math.__default.Max((xs).Select(BigInteger.Zero), Std.Collections.Seq.__default.Max((xs).Drop(BigInteger.One)));
      }
    }
    public static BigInteger Min(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)) == (BigInteger.One)) {
        return (xs).Select(BigInteger.Zero);
      } else {
        return Std.Math.__default.Min((xs).Select(BigInteger.Zero), Std.Collections.Seq.__default.Min((xs).Drop(BigInteger.One)));
      }
    }
    public static Dafny.ISequence<__T> Flatten<__T>(Dafny.ISequence<Dafny.ISequence<__T>> xs) {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, (xs).Select(BigInteger.Zero));
        Dafny.ISequence<Dafny.ISequence<__T>> _in0 = (xs).Drop(BigInteger.One);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<__T> FlattenReverse<__T>(Dafny.ISequence<Dafny.ISequence<__T>> xs) {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(Dafny.Sequence<__T>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(Std.Collections.Seq.__default.Last<Dafny.ISequence<__T>>(xs), _0___accumulator);
        Dafny.ISequence<Dafny.ISequence<__T>> _in0 = Std.Collections.Seq.__default.DropLast<Dafny.ISequence<__T>>(xs);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<__T> Join<__T>(Dafny.ISequence<Dafny.ISequence<__T>> seqs, Dafny.ISequence<__T> separator)
    {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((seqs).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements());
      } else if ((new BigInteger((seqs).Count)) == (BigInteger.One)) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, (seqs).Select(BigInteger.Zero));
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.Concat((seqs).Select(BigInteger.Zero), separator));
        Dafny.ISequence<Dafny.ISequence<__T>> _in0 = (seqs).Drop(BigInteger.One);
        Dafny.ISequence<__T> _in1 = separator;
        seqs = _in0;
        separator = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<Dafny.ISequence<__T>> Split<__T>(Dafny.ISequence<__T> s, __T delim)
    {
      Dafny.ISequence<Dafny.ISequence<__T>> _0___accumulator = Dafny.Sequence<Dafny.ISequence<__T>>.FromElements();
    TAIL_CALL_START: ;
      Std.Wrappers._IOption<BigInteger> _1_i = Std.Collections.Seq.__default.IndexOfOption<__T>(s, delim);
      if ((_1_i).is_Some) {
        _0___accumulator = Dafny.Sequence<Dafny.ISequence<__T>>.Concat(_0___accumulator, Dafny.Sequence<Dafny.ISequence<__T>>.FromElements((s).Take((_1_i).dtor_value)));
        Dafny.ISequence<__T> _in0 = (s).Drop(((_1_i).dtor_value) + (BigInteger.One));
        __T _in1 = delim;
        s = _in0;
        delim = _in1;
        goto TAIL_CALL_START;
      } else {
        return Dafny.Sequence<Dafny.ISequence<__T>>.Concat(_0___accumulator, Dafny.Sequence<Dafny.ISequence<__T>>.FromElements(s));
      }
    }
    public static _System._ITuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>> SplitOnce<__T>(Dafny.ISequence<__T> s, __T delim)
    {
      Std.Wrappers._IOption<BigInteger> _0_i = Std.Collections.Seq.__default.IndexOfOption<__T>(s, delim);
      return _System.Tuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>>.create((s).Take((_0_i).dtor_value), (s).Drop(((_0_i).dtor_value) + (BigInteger.One)));
    }
    public static Std.Wrappers._IOption<_System._ITuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>>> SplitOnceOption<__T>(Dafny.ISequence<__T> s, __T delim)
    {
      Std.Wrappers._IOption<BigInteger> _0_valueOrError0 = Std.Collections.Seq.__default.IndexOfOption<__T>(s, delim);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<_System._ITuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>>>();
      } else {
        BigInteger _1_i = (_0_valueOrError0).Extract();
        return Std.Wrappers.Option<_System._ITuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>>>.create_Some(_System.Tuple2<Dafny.ISequence<__T>, Dafny.ISequence<__T>>.create((s).Take(_1_i), (s).Drop((_1_i) + (BigInteger.One))));
      }
    }
    public static Dafny.ISequence<__R> Map<__T, __R>(Func<__T, __R> f, Dafny.ISequence<__T> xs)
    {
      Dafny.ISequence<__R> _0___accumulator = Dafny.Sequence<__R>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Dafny.Sequence<__R>.Concat(_0___accumulator, Dafny.Sequence<__R>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<__R>.Concat(_0___accumulator, Dafny.Sequence<__R>.FromElements(Dafny.Helpers.Id<Func<__T, __R>>(f)((xs).Select(BigInteger.Zero))));
        Func<__T, __R> _in0 = f;
        Dafny.ISequence<__T> _in1 = (xs).Drop(BigInteger.One);
        f = _in0;
        xs = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<__R> MapPartialFunction<__T, __R>(Func<__T, __R> f, Dafny.ISequence<__T> xs)
    {
      return Std.Collections.Seq.__default.Map<__T, __R>(f, xs);
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<__R>, __E> MapWithResult<__T, __R, __E>(Func<__T, Std.Wrappers._IResult<__R, __E>> f, Dafny.ISequence<__T> xs)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Std.Wrappers.Result<Dafny.ISequence<__R>, __E>.create_Success(Dafny.Sequence<__R>.FromElements());
      } else {
        Std.Wrappers._IResult<__R, __E> _0_valueOrError0 = Dafny.Helpers.Id<Func<__T, Std.Wrappers._IResult<__R, __E>>>(f)((xs).Select(BigInteger.Zero));
        if ((_0_valueOrError0).IsFailure()) {
          return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<__R>>();
        } else {
          __R _1_head = (_0_valueOrError0).Extract();
          Std.Wrappers._IResult<Dafny.ISequence<__R>, __E> _2_valueOrError1 = Std.Collections.Seq.__default.MapWithResult<__T, __R, __E>(f, (xs).Drop(BigInteger.One));
          if ((_2_valueOrError1).IsFailure()) {
            return (_2_valueOrError1).PropagateFailure<Dafny.ISequence<__R>>();
          } else {
            Dafny.ISequence<__R> _3_tail = (_2_valueOrError1).Extract();
            return Std.Wrappers.Result<Dafny.ISequence<__R>, __E>.create_Success(Dafny.Sequence<__R>.Concat(Dafny.Sequence<__R>.FromElements(_1_head), _3_tail));
          }
        }
      }
    }
    public static Dafny.ISequence<__T> Filter<__T>(Func<__T, bool> f, Dafny.ISequence<__T> xs)
    {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, ((Dafny.Helpers.Id<Func<__T, bool>>(f)((xs).Select(BigInteger.Zero))) ? (Dafny.Sequence<__T>.FromElements((xs).Select(BigInteger.Zero))) : (Dafny.Sequence<__T>.FromElements())));
        Func<__T, bool> _in0 = f;
        Dafny.ISequence<__T> _in1 = (xs).Drop(BigInteger.One);
        f = _in0;
        xs = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static __A FoldLeft<__A, __T>(Func<__A, __T, __A> f, __A init, Dafny.ISequence<__T> xs)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return init;
      } else {
        Func<__A, __T, __A> _in0 = f;
        __A _in1 = Dafny.Helpers.Id<Func<__A, __T, __A>>(f)(init, (xs).Select(BigInteger.Zero));
        Dafny.ISequence<__T> _in2 = (xs).Drop(BigInteger.One);
        f = _in0;
        init = _in1;
        xs = _in2;
        goto TAIL_CALL_START;
      }
    }
    public static __A FoldRight<__A, __T>(Func<__T, __A, __A> f, Dafny.ISequence<__T> xs, __A init)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return init;
      } else {
        return Dafny.Helpers.Id<Func<__T, __A, __A>>(f)((xs).Select(BigInteger.Zero), Std.Collections.Seq.__default.FoldRight<__A, __T>(f, (xs).Drop(BigInteger.One), init));
      }
    }
    public static Dafny.ISequence<__T> SetToSeq<__T>(Dafny.ISet<__T> s)
    {
      Dafny.ISequence<__T> xs = Dafny.Sequence<__T>.Empty;
      xs = Dafny.Sequence<__T>.FromElements();
      Dafny.ISet<__T> _0_left;
      _0_left = s;
      while (!(_0_left).Equals(Dafny.Set<__T>.FromElements())) {
        __T _1_x;
        foreach (__T _assign_such_that_0 in (_0_left).Elements) {
          _1_x = (__T)_assign_such_that_0;
          if ((_0_left).Contains(_1_x)) {
            goto after__ASSIGN_SUCH_THAT_0;
          }
        }
        throw new System.Exception("assign-such-that search produced no value");
      after__ASSIGN_SUCH_THAT_0: ;
        _0_left = Dafny.Set<__T>.Difference(_0_left, Dafny.Set<__T>.FromElements(_1_x));
        xs = Dafny.Sequence<__T>.Concat(xs, Dafny.Sequence<__T>.FromElements(_1_x));
      }
      return xs;
    }
    public static Dafny.ISequence<__T> SetToSortedSeq<__T>(Dafny.ISet<__T> s, Func<__T, __T, bool> R)
    {
      Dafny.ISequence<__T> xs = Dafny.Sequence<__T>.Empty;
      Dafny.ISequence<__T> _out0;
      _out0 = Std.Collections.Seq.__default.SetToSeq<__T>(s);
      xs = _out0;
      xs = Std.Collections.Seq.__default.MergeSortBy<__T>(R, xs);
      return xs;
    }
    public static Dafny.ISequence<__T> MergeSortBy<__T>(Func<__T, __T, bool> lessThanOrEq, Dafny.ISequence<__T> a)
    {
      if ((new BigInteger((a).Count)) <= (BigInteger.One)) {
        return a;
      } else {
        BigInteger _0_splitIndex = Dafny.Helpers.EuclideanDivision(new BigInteger((a).Count), new BigInteger(2));
        Dafny.ISequence<__T> _1_left = (a).Take(_0_splitIndex);
        Dafny.ISequence<__T> _2_right = (a).Drop(_0_splitIndex);
        Dafny.ISequence<__T> _3_leftSorted = Std.Collections.Seq.__default.MergeSortBy<__T>(lessThanOrEq, _1_left);
        Dafny.ISequence<__T> _4_rightSorted = Std.Collections.Seq.__default.MergeSortBy<__T>(lessThanOrEq, _2_right);
        return Std.Collections.Seq.__default.MergeSortedWith<__T>(_3_leftSorted, _4_rightSorted, lessThanOrEq);
      }
    }
    public static Dafny.ISequence<__T> MergeSortedWith<__T>(Dafny.ISequence<__T> left, Dafny.ISequence<__T> right, Func<__T, __T, bool> lessThanOrEq)
    {
      Dafny.ISequence<__T> _0___accumulator = Dafny.Sequence<__T>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((left).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, right);
      } else if ((new BigInteger((right).Count)).Sign == 0) {
        return Dafny.Sequence<__T>.Concat(_0___accumulator, left);
      } else if (Dafny.Helpers.Id<Func<__T, __T, bool>>(lessThanOrEq)((left).Select(BigInteger.Zero), (right).Select(BigInteger.Zero))) {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements((left).Select(BigInteger.Zero)));
        Dafny.ISequence<__T> _in0 = (left).Drop(BigInteger.One);
        Dafny.ISequence<__T> _in1 = right;
        Func<__T, __T, bool> _in2 = lessThanOrEq;
        left = _in0;
        right = _in1;
        lessThanOrEq = _in2;
        goto TAIL_CALL_START;
      } else {
        _0___accumulator = Dafny.Sequence<__T>.Concat(_0___accumulator, Dafny.Sequence<__T>.FromElements((right).Select(BigInteger.Zero)));
        Dafny.ISequence<__T> _in3 = left;
        Dafny.ISequence<__T> _in4 = (right).Drop(BigInteger.One);
        Func<__T, __T, bool> _in5 = lessThanOrEq;
        left = _in3;
        right = _in4;
        lessThanOrEq = _in5;
        goto TAIL_CALL_START;
      }
    }
  }
} // end of namespace Std.Collections.Seq
namespace Std.Collections.Array {

  public partial class __default {
    public static Std.Wrappers._IOption<BigInteger> BinarySearch<__T>(__T[] a, __T key, Func<__T, __T, bool> less)
    {
      Std.Wrappers._IOption<BigInteger> r = Std.Wrappers.Option<BigInteger>.Default();
      BigInteger _0_lo;
      BigInteger _1_hi;
      BigInteger _rhs0 = BigInteger.Zero;
      BigInteger _rhs1 = new BigInteger((a).Length);
      _0_lo = _rhs0;
      _1_hi = _rhs1;
      while ((_0_lo) < (_1_hi)) {
        BigInteger _2_mid;
        _2_mid = Dafny.Helpers.EuclideanDivision((_0_lo) + (_1_hi), new BigInteger(2));
        if (Dafny.Helpers.Id<Func<__T, __T, bool>>(less)(key, (a)[(int)(_2_mid)])) {
          _1_hi = _2_mid;
        } else if (Dafny.Helpers.Id<Func<__T, __T, bool>>(less)((a)[(int)(_2_mid)], key)) {
          _0_lo = (_2_mid) + (BigInteger.One);
        } else {
          r = Std.Wrappers.Option<BigInteger>.create_Some(_2_mid);
          return r;
        }
      }
      r = Std.Wrappers.Option<BigInteger>.create_None();
      return r;
      return r;
    }
  }
} // end of namespace Std.Collections.Array
namespace Std.Collections.Imap {

  public partial class __default {
    public static Std.Wrappers._IOption<__Y> Get<__X, __Y>(Dafny.IMap<__X,__Y> m, __X x)
    {
      if ((m).Contains(x)) {
        return Std.Wrappers.Option<__Y>.create_Some(Dafny.Map<__X, __Y>.Select(m,x));
      } else {
        return Std.Wrappers.Option<__Y>.create_None();
      }
    }
  }
} // end of namespace Std.Collections.Imap
namespace Std.Functions {

} // end of namespace Std.Functions
namespace Std.Collections.Iset {

} // end of namespace Std.Collections.Iset
namespace Std.Collections.Map {

  public partial class __default {
    public static Std.Wrappers._IOption<__Y> Get<__X, __Y>(Dafny.IMap<__X,__Y> m, __X x)
    {
      if ((m).Contains(x)) {
        return Std.Wrappers.Option<__Y>.create_Some(Dafny.Map<__X, __Y>.Select(m,x));
      } else {
        return Std.Wrappers.Option<__Y>.create_None();
      }
    }
    public static Dafny.IMap<__X,__Y> ToImap<__X, __Y>(Dafny.IMap<__X,__Y> m) {
      return Dafny.Helpers.Id<Func<Dafny.IMap<__X,__Y>, Dafny.IMap<__X,__Y>>>((_0_m) => ((System.Func<Dafny.IMap<__X,__Y>>)(() => {
        var _coll0 = new System.Collections.Generic.List<Dafny.Pair<__X,__Y>>();
        foreach (__X _compr_0 in (_0_m).Keys.Elements) {
          __X _1_x = (__X)_compr_0;
          if ((_0_m).Contains(_1_x)) {
            _coll0.Add(new Dafny.Pair<__X,__Y>(_1_x, Dafny.Map<__X, __Y>.Select(_0_m,_1_x)));
          }
        }
        return Dafny.Map<__X,__Y>.FromCollection(_coll0);
      }))())(m);
    }
    public static Dafny.IMap<__X,__Y> RemoveKeys<__X, __Y>(Dafny.IMap<__X,__Y> m, Dafny.ISet<__X> xs)
    {
      return Dafny.Map<__X, __Y>.Subtract(m, xs);
    }
    public static Dafny.IMap<__X,__Y> Remove<__X, __Y>(Dafny.IMap<__X,__Y> m, __X x)
    {
      Dafny.IMap<__X,__Y> _0_m_k = Dafny.Helpers.Id<Func<Dafny.IMap<__X,__Y>, __X, Dafny.IMap<__X,__Y>>>((_1_m, _2_x) => ((System.Func<Dafny.IMap<__X,__Y>>)(() => {
        var _coll0 = new System.Collections.Generic.List<Dafny.Pair<__X,__Y>>();
        foreach (__X _compr_0 in (_1_m).Keys.Elements) {
          __X _3_x_k = (__X)_compr_0;
          if (((_1_m).Contains(_3_x_k)) && (!object.Equals(_3_x_k, _2_x))) {
            _coll0.Add(new Dafny.Pair<__X,__Y>(_3_x_k, Dafny.Map<__X, __Y>.Select(_1_m,_3_x_k)));
          }
        }
        return Dafny.Map<__X,__Y>.FromCollection(_coll0);
      }))())(m, x);
      return _0_m_k;
    }
    public static Dafny.IMap<__X,__Y> Restrict<__X, __Y>(Dafny.IMap<__X,__Y> m, Dafny.ISet<__X> xs)
    {
      return Dafny.Helpers.Id<Func<Dafny.ISet<__X>, Dafny.IMap<__X,__Y>, Dafny.IMap<__X,__Y>>>((_0_xs, _1_m) => ((System.Func<Dafny.IMap<__X,__Y>>)(() => {
        var _coll0 = new System.Collections.Generic.List<Dafny.Pair<__X,__Y>>();
        foreach (__X _compr_0 in (_0_xs).Elements) {
          __X _2_x = (__X)_compr_0;
          if (((_0_xs).Contains(_2_x)) && ((_1_m).Contains(_2_x))) {
            _coll0.Add(new Dafny.Pair<__X,__Y>(_2_x, Dafny.Map<__X, __Y>.Select(_1_m,_2_x)));
          }
        }
        return Dafny.Map<__X,__Y>.FromCollection(_coll0);
      }))())(xs, m);
    }
    public static Dafny.IMap<__X,__Y> Union<__X, __Y>(Dafny.IMap<__X,__Y> m, Dafny.IMap<__X,__Y> m_k)
    {
      return Dafny.Map<__X, __Y>.Merge(m, m_k);
    }
  }
} // end of namespace Std.Collections.Map
namespace Std.Collections.Set {

  public partial class __default {
    public static __T ExtractFromSingleton<__T>(Dafny.ISet<__T> s) {
      return Dafny.Helpers.Let<int, __T>(0, _let_dummy_0 =>  {
        __T _0_x = default(__T);
        foreach (__T _assign_such_that_0 in (s).Elements) {
          _0_x = (__T)_assign_such_that_0;
          if ((s).Contains(_0_x)) {
            goto after__ASSIGN_SUCH_THAT_0;
          }
        }
        throw new System.Exception("assign-such-that search produced no value");
      after__ASSIGN_SUCH_THAT_0: ;
        return _0_x;
      }
      );
    }
    public static Dafny.ISet<__Y> Map<__X, __Y>(Func<__X, __Y> f, Dafny.ISet<__X> xs)
    {
      Dafny.ISet<__Y> _0_ys = Dafny.Helpers.Id<Func<Dafny.ISet<__X>, Func<__X, __Y>, Dafny.ISet<__Y>>>((_1_xs, _2_f) => ((System.Func<Dafny.ISet<__Y>>)(() => {
        var _coll0 = new System.Collections.Generic.List<__Y>();
        foreach (__X _compr_0 in (_1_xs).Elements) {
          __X _3_x = (__X)_compr_0;
          if ((_1_xs).Contains(_3_x)) {
            _coll0.Add(Dafny.Helpers.Id<Func<__X, __Y>>(_2_f)(_3_x));
          }
        }
        return Dafny.Set<__Y>.FromCollection(_coll0);
      }))())(xs, f);
      return _0_ys;
    }
    public static Dafny.ISet<__X> Filter<__X>(Func<__X, bool> f, Dafny.ISet<__X> xs)
    {
      Dafny.ISet<__X> _0_ys = Dafny.Helpers.Id<Func<Dafny.ISet<__X>, Func<__X, bool>, Dafny.ISet<__X>>>((_1_xs, _2_f) => ((System.Func<Dafny.ISet<__X>>)(() => {
        var _coll0 = new System.Collections.Generic.List<__X>();
        foreach (__X _compr_0 in (_1_xs).Elements) {
          __X _3_x = (__X)_compr_0;
          if (((_1_xs).Contains(_3_x)) && (Dafny.Helpers.Id<Func<__X, bool>>(_2_f)(_3_x))) {
            _coll0.Add(_3_x);
          }
        }
        return Dafny.Set<__X>.FromCollection(_coll0);
      }))())(xs, f);
      return _0_ys;
    }
    public static Dafny.ISet<BigInteger> SetRange(BigInteger a, BigInteger b)
    {
      Dafny.ISet<BigInteger> _0___accumulator = Dafny.Set<BigInteger>.FromElements();
    TAIL_CALL_START: ;
      if ((a) == (b)) {
        return Dafny.Set<BigInteger>.Union(Dafny.Set<BigInteger>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Set<BigInteger>.Union(_0___accumulator, Dafny.Set<BigInteger>.FromElements(a));
        BigInteger _in0 = (a) + (BigInteger.One);
        BigInteger _in1 = b;
        a = _in0;
        b = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISet<BigInteger> SetRangeZeroBound(BigInteger n) {
      return Std.Collections.Set.__default.SetRange(BigInteger.Zero, n);
    }
  }
} // end of namespace Std.Collections.Set
namespace Std.Collections {

} // end of namespace Std.Collections
namespace Std.DynamicArray {


  public partial class DynamicArray<A> {
    public DynamicArray() {
      this.size = BigInteger.Zero;
      this.capacity = BigInteger.Zero;
      this.data = new A[0];
    }
    public BigInteger size {get; set;}
    public BigInteger capacity {get; set;}
    public A[] data {get; set;}
    public void __ctor()
    {
      (this).size = BigInteger.Zero;
      (this).capacity = BigInteger.Zero;
      A[] _nw0 = new A[Dafny.Helpers.ToIntChecked(BigInteger.Zero, "array size exceeds memory limit")];
      (this).data = _nw0;
    }
    public A At(BigInteger index) {
      return (this.data)[(int)(index)];
    }
    public void Put(BigInteger index, A element)
    {
      A[] _arr0 = this.data;
      _arr0[(int)((index))] = element;
    }
    public void Ensure(BigInteger reserved, A defaultValue)
    {
      BigInteger _0_newCapacity;
      _0_newCapacity = this.capacity;
      while ((reserved) > ((_0_newCapacity) - (this.size))) {
        _0_newCapacity = (this).DefaultNewCapacity(_0_newCapacity);
      }
      if ((_0_newCapacity) > (this.capacity)) {
        (this).Realloc(defaultValue, _0_newCapacity);
      }
    }
    public void PopFast()
    {
      (this).size = (this.size) - (BigInteger.One);
    }
    public void PushFast(A element)
    {
      A[] _arr0 = this.data;
      BigInteger _index0 = this.size;
      _arr0[(int)(_index0)] = element;
      (this).size = (this.size) + (BigInteger.One);
    }
    public void Push(A element)
    {
      if ((this.size) == (this.capacity)) {
        (this).ReallocDefault(element);
      }
      (this).PushFast(element);
    }
    public void Realloc(A defaultValue, BigInteger newCapacity)
    {
      A[] _0_oldData;
      BigInteger _1_oldCapacity;
      A[] _rhs0 = this.data;
      BigInteger _rhs1 = this.capacity;
      _0_oldData = _rhs0;
      _1_oldCapacity = _rhs1;
      Func<BigInteger, A> _init0 = Dafny.Helpers.Id<Func<A, Func<BigInteger, A>>>((_2_defaultValue) => ((System.Func<BigInteger, A>)((_3___v0) => {
        return _2_defaultValue;
      })))(defaultValue);
      A[] _nw0 = new A[Dafny.Helpers.ToIntChecked(newCapacity, "array size exceeds memory limit")];
      for (var _i0_0 = 0; _i0_0 < new BigInteger(_nw0.Length); _i0_0++) {
        _nw0[(int)(_i0_0)] = _init0(_i0_0);
      }
      A[] _rhs2 = _nw0;
      BigInteger _rhs3 = newCapacity;
      Std.DynamicArray.DynamicArray<A> _lhs0 = this;
      Std.DynamicArray.DynamicArray<A> _lhs1 = this;
      _lhs0.data = _rhs2;
      _lhs1.capacity = _rhs3;
      (this).CopyFrom(_0_oldData, _1_oldCapacity);
    }
    public BigInteger DefaultNewCapacity(BigInteger capacity) {
      if ((capacity).Sign == 0) {
        return new BigInteger(8);
      } else {
        return (new BigInteger(2)) * (capacity);
      }
    }
    public void ReallocDefault(A defaultValue)
    {
      (this).Realloc(defaultValue, (this).DefaultNewCapacity(this.capacity));
    }
    public void CopyFrom(A[] newData, BigInteger count)
    {
      foreach (BigInteger _guard_loop_0 in Dafny.Helpers.IntegerRange(BigInteger.Zero, count)) {
        BigInteger _0_index = (BigInteger)_guard_loop_0;
        if ((true) && (((_0_index).Sign != -1) && ((_0_index) < (count)))) {
          A[] _arr0 = this.data;
          _arr0[(int)((_0_index))] = (newData)[(int)(_0_index)];
        }
      }
    }
  }
} // end of namespace Std.DynamicArray
namespace Std.Arithmetic.GeneralInternals {

} // end of namespace Std.Arithmetic.GeneralInternals
namespace Std.Arithmetic.MulInternalsNonlinear {

} // end of namespace Std.Arithmetic.MulInternalsNonlinear
namespace Std.Arithmetic.MulInternals {

  public partial class __default {
    public static BigInteger MulPos(BigInteger x, BigInteger y)
    {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((x).Sign == 0) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (_0___accumulator) + (y);
        BigInteger _in0 = (x) - (BigInteger.One);
        BigInteger _in1 = y;
        x = _in0;
        y = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static BigInteger MulRecursive(BigInteger x, BigInteger y)
    {
      if ((x).Sign != -1) {
        return Std.Arithmetic.MulInternals.__default.MulPos(x, y);
      } else {
        return (new BigInteger(-1)) * (Std.Arithmetic.MulInternals.__default.MulPos((new BigInteger(-1)) * (x), y));
      }
    }
  }
} // end of namespace Std.Arithmetic.MulInternals
namespace Std.Arithmetic.Mul {

} // end of namespace Std.Arithmetic.Mul
namespace Std.Arithmetic.ModInternalsNonlinear {

} // end of namespace Std.Arithmetic.ModInternalsNonlinear
namespace Std.Arithmetic.DivInternalsNonlinear {

} // end of namespace Std.Arithmetic.DivInternalsNonlinear
namespace Std.Arithmetic.ModInternals {

  public partial class __default {
    public static BigInteger ModRecursive(BigInteger x, BigInteger d)
    {
    TAIL_CALL_START: ;
      if ((x).Sign == -1) {
        BigInteger _in0 = (d) + (x);
        BigInteger _in1 = d;
        x = _in0;
        d = _in1;
        goto TAIL_CALL_START;
      } else if ((x) < (d)) {
        return x;
      } else {
        BigInteger _in2 = (x) - (d);
        BigInteger _in3 = d;
        x = _in2;
        d = _in3;
        goto TAIL_CALL_START;
      }
    }
  }
} // end of namespace Std.Arithmetic.ModInternals
namespace Std.Arithmetic.DivInternals {

  public partial class __default {
    public static BigInteger DivPos(BigInteger x, BigInteger d)
    {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((x).Sign == -1) {
        _0___accumulator = (_0___accumulator) + (new BigInteger(-1));
        BigInteger _in0 = (x) + (d);
        BigInteger _in1 = d;
        x = _in0;
        d = _in1;
        goto TAIL_CALL_START;
      } else if ((x) < (d)) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (_0___accumulator) + (BigInteger.One);
        BigInteger _in2 = (x) - (d);
        BigInteger _in3 = d;
        x = _in2;
        d = _in3;
        goto TAIL_CALL_START;
      }
    }
    public static BigInteger DivRecursive(BigInteger x, BigInteger d)
    {
      if ((d).Sign == 1) {
        return Std.Arithmetic.DivInternals.__default.DivPos(x, d);
      } else {
        return (new BigInteger(-1)) * (Std.Arithmetic.DivInternals.__default.DivPos(x, (new BigInteger(-1)) * (d)));
      }
    }
  }
} // end of namespace Std.Arithmetic.DivInternals
namespace Std.Arithmetic.DivMod {

  public partial class __default {
    public static bool MultiplesVanish(BigInteger a, BigInteger b, BigInteger m)
    {
      return (Dafny.Helpers.EuclideanModulus(((m) * (a)) + (b), m)) == (Dafny.Helpers.EuclideanModulus(b, m));
    }
  }
} // end of namespace Std.Arithmetic.DivMod
namespace Std.Arithmetic.Power {

  public partial class __default {
    public static BigInteger Pow(BigInteger b, BigInteger e)
    {
      BigInteger _0___accumulator = BigInteger.One;
    TAIL_CALL_START: ;
      if ((e).Sign == 0) {
        return (BigInteger.One) * (_0___accumulator);
      } else {
        _0___accumulator = (_0___accumulator) * (b);
        BigInteger _in0 = b;
        BigInteger _in1 = (e) - (BigInteger.One);
        b = _in0;
        e = _in1;
        goto TAIL_CALL_START;
      }
    }
  }
} // end of namespace Std.Arithmetic.Power
namespace Std.Arithmetic.Logarithm {

  public partial class __default {
    public static BigInteger Log(BigInteger @base, BigInteger pow)
    {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((pow) < (@base)) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (_0___accumulator) + (BigInteger.One);
        BigInteger _in0 = @base;
        BigInteger _in1 = Dafny.Helpers.EuclideanDivision(pow, @base);
        @base = _in0;
        pow = _in1;
        goto TAIL_CALL_START;
      }
    }
  }
} // end of namespace Std.Arithmetic.Logarithm
namespace Std.Arithmetic.Power2 {

  public partial class __default {
    public static BigInteger Pow2(BigInteger e) {
      return Std.Arithmetic.Power.__default.Pow(new BigInteger(2), e);
    }
  }
} // end of namespace Std.Arithmetic.Power2
namespace Std.Strings.HexConversion {

  public partial class __default {
    public static BigInteger BASE() {
      return Std.Strings.HexConversion.__default.@base;
    }
    public static bool IsDigitChar(Dafny.Rune c) {
      return (Std.Strings.HexConversion.__default.charToDigit).Contains(c);
    }
    public static Dafny.ISequence<Dafny.Rune> OfDigits(Dafny.ISequence<BigInteger> digits) {
      Dafny.ISequence<Dafny.Rune> _0___accumulator = Dafny.Sequence<Dafny.Rune>.FromElements();
    TAIL_CALL_START: ;
      if ((digits).Equals(Dafny.Sequence<BigInteger>.FromElements())) {
        return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements((Std.Strings.HexConversion.__default.chars).Select((digits).Select(BigInteger.Zero))), _0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = (digits).Drop(BigInteger.One);
        digits = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<Dafny.Rune> OfNat(BigInteger n) {
      if ((n).Sign == 0) {
        return Dafny.Sequence<Dafny.Rune>.FromElements((Std.Strings.HexConversion.__default.chars).Select(BigInteger.Zero));
      } else {
        return Std.Strings.HexConversion.__default.OfDigits(Std.Strings.HexConversion.__default.FromNat(n));
      }
    }
    public static bool IsNumberStr(Dafny.ISequence<Dafny.Rune> str, Dafny.Rune minus)
    {
      return !(!(str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) || (((((str).Select(BigInteger.Zero)) == (minus)) || ((Std.Strings.HexConversion.__default.charToDigit).Contains((str).Select(BigInteger.Zero)))) && (Dafny.Helpers.Id<Func<Dafny.ISequence<Dafny.Rune>, bool>>((_0_str) => Dafny.Helpers.Quantifier<Dafny.Rune>(((_0_str).Drop(BigInteger.One)).UniqueElements, true, (((_forall_var_0) => {
        Dafny.Rune _1_c = (Dafny.Rune)_forall_var_0;
        return !(((_0_str).Drop(BigInteger.One)).Contains(_1_c)) || (Std.Strings.HexConversion.__default.IsDigitChar(_1_c));
      }))))(str)));
    }
    public static Dafny.ISequence<Dafny.Rune> OfInt(BigInteger n, Dafny.Rune minus)
    {
      if ((n).Sign != -1) {
        return Std.Strings.HexConversion.__default.OfNat(n);
      } else {
        return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements(minus), Std.Strings.HexConversion.__default.OfNat((BigInteger.Zero) - (n)));
      }
    }
    public static BigInteger ToNat(Dafny.ISequence<Dafny.Rune> str) {
      if ((str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) {
        return BigInteger.Zero;
      } else {
        Dafny.Rune _0_c = (str).Select((new BigInteger((str).Count)) - (BigInteger.One));
        return ((Std.Strings.HexConversion.__default.ToNat((str).Take((new BigInteger((str).Count)) - (BigInteger.One)))) * (Std.Strings.HexConversion.__default.@base)) + (Dafny.Map<Dafny.Rune, BigInteger>.Select(Std.Strings.HexConversion.__default.charToDigit,_0_c));
      }
    }
    public static BigInteger ToInt(Dafny.ISequence<Dafny.Rune> str, Dafny.Rune minus)
    {
      if (Dafny.Sequence<Dafny.Rune>.IsPrefixOf(Dafny.Sequence<Dafny.Rune>.FromElements(minus), str)) {
        return (BigInteger.Zero) - (Std.Strings.HexConversion.__default.ToNat((str).Drop(BigInteger.One)));
      } else {
        return Std.Strings.HexConversion.__default.ToNat(str);
      }
    }
    public static BigInteger ToNatRight(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return BigInteger.Zero;
      } else {
        return ((Std.Strings.HexConversion.__default.ToNatRight(Std.Collections.Seq.__default.DropFirst<BigInteger>(xs))) * (Std.Strings.HexConversion.__default.BASE())) + (Std.Collections.Seq.__default.First<BigInteger>(xs));
      }
    }
    public static BigInteger ToNatLeft(Dafny.ISequence<BigInteger> xs) {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) * (Std.Arithmetic.Power.__default.Pow(Std.Strings.HexConversion.__default.BASE(), (new BigInteger((xs).Count)) - (BigInteger.One)))) + (_0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = Std.Collections.Seq.__default.DropLast<BigInteger>(xs);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> FromNat(BigInteger n) {
      Dafny.ISequence<BigInteger> _0___accumulator = Dafny.Sequence<BigInteger>.FromElements();
    TAIL_CALL_START: ;
      if ((n).Sign == 0) {
        return Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements(Dafny.Helpers.EuclideanModulus(n, Std.Strings.HexConversion.__default.BASE())));
        BigInteger _in0 = Dafny.Helpers.EuclideanDivision(n, Std.Strings.HexConversion.__default.BASE());
        n = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtend(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)) >= (n)) {
        return xs;
      } else {
        Dafny.ISequence<BigInteger> _in0 = Dafny.Sequence<BigInteger>.Concat(xs, Dafny.Sequence<BigInteger>.FromElements(BigInteger.Zero));
        BigInteger _in1 = n;
        xs = _in0;
        n = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtendMultiple(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
      BigInteger _0_newLen = ((new BigInteger((xs).Count)) + (n)) - (Dafny.Helpers.EuclideanModulus(new BigInteger((xs).Count), n));
      return Std.Strings.HexConversion.__default.SeqExtend(xs, _0_newLen);
    }
    public static Dafny.ISequence<BigInteger> FromNatWithLen(BigInteger n, BigInteger len)
    {
      return Std.Strings.HexConversion.__default.SeqExtend(Std.Strings.HexConversion.__default.FromNat(n), len);
    }
    public static Dafny.ISequence<BigInteger> SeqZero(BigInteger len) {
      Dafny.ISequence<BigInteger> _0_xs = Std.Strings.HexConversion.__default.FromNatWithLen(BigInteger.Zero, len);
      return _0_xs;
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqAdd(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.Strings.HexConversion.__default.SeqAdd(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs_k = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        BigInteger _2_sum = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) + (Std.Collections.Seq.__default.Last<BigInteger>(ys))) + (_1_cin);
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((_2_sum) < (Std.Strings.HexConversion.__default.BASE())) ? (_System.Tuple2<BigInteger, BigInteger>.create(_2_sum, BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((_2_sum) - (Std.Strings.HexConversion.__default.BASE()), BigInteger.One)));
        BigInteger _3_sum__out = _let_tmp_rhs1.dtor__0;
        BigInteger _4_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs_k, Dafny.Sequence<BigInteger>.FromElements(_3_sum__out)), _4_cout);
      }
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqSub(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.Strings.HexConversion.__default.SeqSub(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((Std.Collections.Seq.__default.Last<BigInteger>(xs)) >= ((Std.Collections.Seq.__default.Last<BigInteger>(ys)) + (_1_cin))) ? (_System.Tuple2<BigInteger, BigInteger>.create(((Std.Collections.Seq.__default.Last<BigInteger>(xs)) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((((Std.Strings.HexConversion.__default.BASE()) + (Std.Collections.Seq.__default.Last<BigInteger>(xs))) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.One)));
        BigInteger _2_diff__out = _let_tmp_rhs1.dtor__0;
        BigInteger _3_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs, Dafny.Sequence<BigInteger>.FromElements(_2_diff__out)), _3_cout);
      }
    }
    public static Dafny.ISequence<Dafny.Rune> HEX__DIGITS { get {
      return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("0123456789ABCDEF");
    } }
    public static Dafny.ISequence<Dafny.Rune> chars { get {
      return Std.Strings.HexConversion.__default.HEX__DIGITS;
    } }
    public static BigInteger @base { get {
      return new BigInteger((Std.Strings.HexConversion.__default.chars).Count);
    } }
    public static Dafny.IMap<Dafny.Rune,BigInteger> charToDigit { get {
      return Dafny.Map<Dafny.Rune, BigInteger>.FromElements(new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('0'), BigInteger.Zero), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('1'), BigInteger.One), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('2'), new BigInteger(2)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('3'), new BigInteger(3)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('4'), new BigInteger(4)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('5'), new BigInteger(5)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('6'), new BigInteger(6)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('7'), new BigInteger(7)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('8'), new BigInteger(8)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('9'), new BigInteger(9)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('a'), new BigInteger(10)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('b'), new BigInteger(11)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('c'), new BigInteger(12)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('d'), new BigInteger(13)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('e'), new BigInteger(14)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('f'), new BigInteger(15)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('A'), new BigInteger(10)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('B'), new BigInteger(11)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('C'), new BigInteger(12)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('D'), new BigInteger(13)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('E'), new BigInteger(14)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('F'), new BigInteger(15)));
    } }
  }

  public partial class CharSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>>(Dafny.Sequence<Dafny.Rune>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<Dafny.Rune> __source) {
      Dafny.ISequence<Dafny.Rune> _0_chars = __source;
      return (new BigInteger((_0_chars).Count)) > (BigInteger.One);
    }
  }

  public partial class digit {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _1_i = __source;
      if (_System.nat._Is(_1_i)) {
        return ((_1_i).Sign != -1) && ((_1_i) < (Std.Strings.HexConversion.__default.BASE()));
      }
      return false;
    }
  }
} // end of namespace Std.Strings.HexConversion
namespace Std.Strings.DecimalConversion {

  public partial class __default {
    public static BigInteger BASE() {
      return Std.Strings.DecimalConversion.__default.@base;
    }
    public static bool IsDigitChar(Dafny.Rune c) {
      return (Std.Strings.DecimalConversion.__default.charToDigit).Contains(c);
    }
    public static Dafny.ISequence<Dafny.Rune> OfDigits(Dafny.ISequence<BigInteger> digits) {
      Dafny.ISequence<Dafny.Rune> _0___accumulator = Dafny.Sequence<Dafny.Rune>.FromElements();
    TAIL_CALL_START: ;
      if ((digits).Equals(Dafny.Sequence<BigInteger>.FromElements())) {
        return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements((Std.Strings.DecimalConversion.__default.chars).Select((digits).Select(BigInteger.Zero))), _0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = (digits).Drop(BigInteger.One);
        digits = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<Dafny.Rune> OfNat(BigInteger n) {
      if ((n).Sign == 0) {
        return Dafny.Sequence<Dafny.Rune>.FromElements((Std.Strings.DecimalConversion.__default.chars).Select(BigInteger.Zero));
      } else {
        return Std.Strings.DecimalConversion.__default.OfDigits(Std.Strings.DecimalConversion.__default.FromNat(n));
      }
    }
    public static bool IsNumberStr(Dafny.ISequence<Dafny.Rune> str, Dafny.Rune minus)
    {
      return !(!(str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) || (((((str).Select(BigInteger.Zero)) == (minus)) || ((Std.Strings.DecimalConversion.__default.charToDigit).Contains((str).Select(BigInteger.Zero)))) && (Dafny.Helpers.Id<Func<Dafny.ISequence<Dafny.Rune>, bool>>((_0_str) => Dafny.Helpers.Quantifier<Dafny.Rune>(((_0_str).Drop(BigInteger.One)).UniqueElements, true, (((_forall_var_0) => {
        Dafny.Rune _1_c = (Dafny.Rune)_forall_var_0;
        return !(((_0_str).Drop(BigInteger.One)).Contains(_1_c)) || (Std.Strings.DecimalConversion.__default.IsDigitChar(_1_c));
      }))))(str)));
    }
    public static Dafny.ISequence<Dafny.Rune> OfInt(BigInteger n, Dafny.Rune minus)
    {
      if ((n).Sign != -1) {
        return Std.Strings.DecimalConversion.__default.OfNat(n);
      } else {
        return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements(minus), Std.Strings.DecimalConversion.__default.OfNat((BigInteger.Zero) - (n)));
      }
    }
    public static BigInteger ToNat(Dafny.ISequence<Dafny.Rune> str) {
      if ((str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) {
        return BigInteger.Zero;
      } else {
        Dafny.Rune _0_c = (str).Select((new BigInteger((str).Count)) - (BigInteger.One));
        return ((Std.Strings.DecimalConversion.__default.ToNat((str).Take((new BigInteger((str).Count)) - (BigInteger.One)))) * (Std.Strings.DecimalConversion.__default.@base)) + (Dafny.Map<Dafny.Rune, BigInteger>.Select(Std.Strings.DecimalConversion.__default.charToDigit,_0_c));
      }
    }
    public static BigInteger ToInt(Dafny.ISequence<Dafny.Rune> str, Dafny.Rune minus)
    {
      if (Dafny.Sequence<Dafny.Rune>.IsPrefixOf(Dafny.Sequence<Dafny.Rune>.FromElements(minus), str)) {
        return (BigInteger.Zero) - (Std.Strings.DecimalConversion.__default.ToNat((str).Drop(BigInteger.One)));
      } else {
        return Std.Strings.DecimalConversion.__default.ToNat(str);
      }
    }
    public static BigInteger ToNatRight(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return BigInteger.Zero;
      } else {
        return ((Std.Strings.DecimalConversion.__default.ToNatRight(Std.Collections.Seq.__default.DropFirst<BigInteger>(xs))) * (Std.Strings.DecimalConversion.__default.BASE())) + (Std.Collections.Seq.__default.First<BigInteger>(xs));
      }
    }
    public static BigInteger ToNatLeft(Dafny.ISequence<BigInteger> xs) {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) * (Std.Arithmetic.Power.__default.Pow(Std.Strings.DecimalConversion.__default.BASE(), (new BigInteger((xs).Count)) - (BigInteger.One)))) + (_0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = Std.Collections.Seq.__default.DropLast<BigInteger>(xs);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> FromNat(BigInteger n) {
      Dafny.ISequence<BigInteger> _0___accumulator = Dafny.Sequence<BigInteger>.FromElements();
    TAIL_CALL_START: ;
      if ((n).Sign == 0) {
        return Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements(Dafny.Helpers.EuclideanModulus(n, Std.Strings.DecimalConversion.__default.BASE())));
        BigInteger _in0 = Dafny.Helpers.EuclideanDivision(n, Std.Strings.DecimalConversion.__default.BASE());
        n = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtend(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)) >= (n)) {
        return xs;
      } else {
        Dafny.ISequence<BigInteger> _in0 = Dafny.Sequence<BigInteger>.Concat(xs, Dafny.Sequence<BigInteger>.FromElements(BigInteger.Zero));
        BigInteger _in1 = n;
        xs = _in0;
        n = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtendMultiple(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
      BigInteger _0_newLen = ((new BigInteger((xs).Count)) + (n)) - (Dafny.Helpers.EuclideanModulus(new BigInteger((xs).Count), n));
      return Std.Strings.DecimalConversion.__default.SeqExtend(xs, _0_newLen);
    }
    public static Dafny.ISequence<BigInteger> FromNatWithLen(BigInteger n, BigInteger len)
    {
      return Std.Strings.DecimalConversion.__default.SeqExtend(Std.Strings.DecimalConversion.__default.FromNat(n), len);
    }
    public static Dafny.ISequence<BigInteger> SeqZero(BigInteger len) {
      Dafny.ISequence<BigInteger> _0_xs = Std.Strings.DecimalConversion.__default.FromNatWithLen(BigInteger.Zero, len);
      return _0_xs;
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqAdd(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.Strings.DecimalConversion.__default.SeqAdd(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs_k = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        BigInteger _2_sum = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) + (Std.Collections.Seq.__default.Last<BigInteger>(ys))) + (_1_cin);
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((_2_sum) < (Std.Strings.DecimalConversion.__default.BASE())) ? (_System.Tuple2<BigInteger, BigInteger>.create(_2_sum, BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((_2_sum) - (Std.Strings.DecimalConversion.__default.BASE()), BigInteger.One)));
        BigInteger _3_sum__out = _let_tmp_rhs1.dtor__0;
        BigInteger _4_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs_k, Dafny.Sequence<BigInteger>.FromElements(_3_sum__out)), _4_cout);
      }
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqSub(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.Strings.DecimalConversion.__default.SeqSub(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((Std.Collections.Seq.__default.Last<BigInteger>(xs)) >= ((Std.Collections.Seq.__default.Last<BigInteger>(ys)) + (_1_cin))) ? (_System.Tuple2<BigInteger, BigInteger>.create(((Std.Collections.Seq.__default.Last<BigInteger>(xs)) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((((Std.Strings.DecimalConversion.__default.BASE()) + (Std.Collections.Seq.__default.Last<BigInteger>(xs))) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.One)));
        BigInteger _2_diff__out = _let_tmp_rhs1.dtor__0;
        BigInteger _3_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs, Dafny.Sequence<BigInteger>.FromElements(_2_diff__out)), _3_cout);
      }
    }
    public static Dafny.ISequence<Dafny.Rune> DIGITS { get {
      return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("0123456789");
    } }
    public static Dafny.ISequence<Dafny.Rune> chars { get {
      return Std.Strings.DecimalConversion.__default.DIGITS;
    } }
    public static BigInteger @base { get {
      return new BigInteger((Std.Strings.DecimalConversion.__default.chars).Count);
    } }
    public static Dafny.IMap<Dafny.Rune,BigInteger> charToDigit { get {
      return Dafny.Map<Dafny.Rune, BigInteger>.FromElements(new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('0'), BigInteger.Zero), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('1'), BigInteger.One), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('2'), new BigInteger(2)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('3'), new BigInteger(3)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('4'), new BigInteger(4)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('5'), new BigInteger(5)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('6'), new BigInteger(6)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('7'), new BigInteger(7)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('8'), new BigInteger(8)), new Dafny.Pair<Dafny.Rune, BigInteger>(new Dafny.Rune('9'), new BigInteger(9)));
    } }
  }

  public partial class CharSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>>(Dafny.Sequence<Dafny.Rune>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<Dafny.Rune> __source) {
      Dafny.ISequence<Dafny.Rune> _0_chars = __source;
      return (new BigInteger((_0_chars).Count)) > (BigInteger.One);
    }
  }

  public partial class digit {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _1_i = __source;
      if (_System.nat._Is(_1_i)) {
        return ((_1_i).Sign != -1) && ((_1_i) < (Std.Strings.DecimalConversion.__default.BASE()));
      }
      return false;
    }
  }
} // end of namespace Std.Strings.DecimalConversion
namespace Std.Strings.CharStrEscaping {

  public partial class __default {
    public static Dafny.ISequence<Dafny.Rune> Escape(Dafny.ISequence<Dafny.Rune> str, Dafny.ISet<Dafny.Rune> mustEscape, Dafny.Rune escape)
    {
      Dafny.ISequence<Dafny.Rune> _0___accumulator = Dafny.Sequence<Dafny.Rune>.FromElements();
    TAIL_CALL_START: ;
      if ((str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) {
        return Dafny.Sequence<Dafny.Rune>.Concat(_0___accumulator, str);
      } else if ((mustEscape).Contains((str).Select(BigInteger.Zero))) {
        _0___accumulator = Dafny.Sequence<Dafny.Rune>.Concat(_0___accumulator, Dafny.Sequence<Dafny.Rune>.FromElements(escape, (str).Select(BigInteger.Zero)));
        Dafny.ISequence<Dafny.Rune> _in0 = (str).Drop(BigInteger.One);
        Dafny.ISet<Dafny.Rune> _in1 = mustEscape;
        Dafny.Rune _in2 = escape;
        str = _in0;
        mustEscape = _in1;
        escape = _in2;
        goto TAIL_CALL_START;
      } else {
        _0___accumulator = Dafny.Sequence<Dafny.Rune>.Concat(_0___accumulator, Dafny.Sequence<Dafny.Rune>.FromElements((str).Select(BigInteger.Zero)));
        Dafny.ISequence<Dafny.Rune> _in3 = (str).Drop(BigInteger.One);
        Dafny.ISet<Dafny.Rune> _in4 = mustEscape;
        Dafny.Rune _in5 = escape;
        str = _in3;
        mustEscape = _in4;
        escape = _in5;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> Unescape(Dafny.ISequence<Dafny.Rune> str, Dafny.Rune escape)
    {
      if ((str).Equals(Dafny.Sequence<Dafny.Rune>.FromElements())) {
        return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(str);
      } else if (((str).Select(BigInteger.Zero)) == (escape)) {
        if ((new BigInteger((str).Count)) > (BigInteger.One)) {
          Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> _0_valueOrError0 = Std.Strings.CharStrEscaping.__default.Unescape((str).Drop(new BigInteger(2)), escape);
          if ((_0_valueOrError0).IsFailure()) {
            return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
          } else {
            Dafny.ISequence<Dafny.Rune> _1_tl = (_0_valueOrError0).Extract();
            return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements((str).Select(BigInteger.One)), _1_tl));
          }
        } else {
          return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_None();
        }
      } else {
        Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> _2_valueOrError1 = Std.Strings.CharStrEscaping.__default.Unescape((str).Drop(BigInteger.One), escape);
        if ((_2_valueOrError1).IsFailure()) {
          return (_2_valueOrError1).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
        } else {
          Dafny.ISequence<Dafny.Rune> _3_tl = (_2_valueOrError1).Extract();
          return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.FromElements((str).Select(BigInteger.Zero)), _3_tl));
        }
      }
    }
  }
} // end of namespace Std.Strings.CharStrEscaping
namespace Std.Strings {

  public partial class __default {
    public static Dafny.ISequence<Dafny.Rune> OfNat(BigInteger n) {
      return Std.Strings.DecimalConversion.__default.OfNat(n);
    }
    public static Dafny.ISequence<Dafny.Rune> OfInt(BigInteger n) {
      return Std.Strings.DecimalConversion.__default.OfInt(n, new Dafny.Rune('-'));
    }
    public static BigInteger ToNat(Dafny.ISequence<Dafny.Rune> str) {
      return Std.Strings.DecimalConversion.__default.ToNat(str);
    }
    public static BigInteger ToInt(Dafny.ISequence<Dafny.Rune> str) {
      return Std.Strings.DecimalConversion.__default.ToInt(str, new Dafny.Rune('-'));
    }
    public static Dafny.ISequence<Dafny.Rune> EscapeQuotes(Dafny.ISequence<Dafny.Rune> str) {
      return Std.Strings.CharStrEscaping.__default.Escape(str, Dafny.Set<Dafny.Rune>.FromElements(new Dafny.Rune('\"'), new Dafny.Rune('\'')), new Dafny.Rune('\\'));
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> UnescapeQuotes(Dafny.ISequence<Dafny.Rune> str) {
      return Std.Strings.CharStrEscaping.__default.Unescape(str, new Dafny.Rune('\\'));
    }
    public static Dafny.ISequence<Dafny.Rune> OfBool(bool b) {
      if (b) {
        return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("true");
      } else {
        return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("false");
      }
    }
    public static Dafny.ISequence<Dafny.Rune> OfChar(Dafny.Rune c) {
      return Dafny.Sequence<Dafny.Rune>.FromElements(c);
    }
  }
} // end of namespace Std.Strings
namespace Std.Unicode.Base {

  public partial class __default {
    public static bool IsInAssignedPlane(uint i) {
      byte _0_plane = (byte)((i) >> ((int)((byte)(16))));
      return (Std.Unicode.Base.__default.ASSIGNED__PLANES).Contains(_0_plane);
    }
    public static uint HIGH__SURROGATE__MIN { get {
      return 55296U;
    } }
    public static uint HIGH__SURROGATE__MAX { get {
      return 56319U;
    } }
    public static uint LOW__SURROGATE__MIN { get {
      return 56320U;
    } }
    public static uint LOW__SURROGATE__MAX { get {
      return 57343U;
    } }
    public static Dafny.ISet<byte> ASSIGNED__PLANES { get {
      return Dafny.Set<byte>.FromElements((byte)(0), (byte)(1), (byte)(2), (byte)(3), (byte)(14), (byte)(15), (byte)(16));
    } }
  }

  public partial class CodePoint {
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(0);
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      uint _0_i = (uint)(__source);
      return ((0U) <= (_0_i)) && ((_0_i) <= (1114111U));
    }
  }

  public partial class HighSurrogateCodePoint {
    private static readonly uint Witness = Std.Unicode.Base.__default.HIGH__SURROGATE__MIN;
    public static uint Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(Std.Unicode.Base.HighSurrogateCodePoint.Default());
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      uint _1_p = (uint)(__source);
      if (Std.Unicode.Base.CodePoint._Is(_1_p)) {
        return ((Std.Unicode.Base.__default.HIGH__SURROGATE__MIN) <= (_1_p)) && ((_1_p) <= (Std.Unicode.Base.__default.HIGH__SURROGATE__MAX));
      }
      return false;
    }
  }

  public partial class LowSurrogateCodePoint {
    private static readonly uint Witness = Std.Unicode.Base.__default.LOW__SURROGATE__MIN;
    public static uint Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(Std.Unicode.Base.LowSurrogateCodePoint.Default());
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      uint _2_p = (uint)(__source);
      if (Std.Unicode.Base.CodePoint._Is(_2_p)) {
        return ((Std.Unicode.Base.__default.LOW__SURROGATE__MIN) <= (_2_p)) && ((_2_p) <= (Std.Unicode.Base.__default.LOW__SURROGATE__MAX));
      }
      return false;
    }
  }

  public partial class ScalarValue {
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(0);
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      uint _3_p = (uint)(__source);
      if (Std.Unicode.Base.CodePoint._Is(_3_p)) {
        return (((_3_p) < (Std.Unicode.Base.__default.HIGH__SURROGATE__MIN)) || ((_3_p) > (Std.Unicode.Base.__default.HIGH__SURROGATE__MAX))) && (((_3_p) < (Std.Unicode.Base.__default.LOW__SURROGATE__MIN)) || ((_3_p) > (Std.Unicode.Base.__default.LOW__SURROGATE__MAX)));
      }
      return false;
    }
  }

  public partial class AssignedCodePoint {
    private static readonly Dafny.TypeDescriptor<uint> _TYPE = new Dafny.TypeDescriptor<uint>(0);
    public static Dafny.TypeDescriptor<uint> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(uint __source) {
      uint _4_p = (uint)(__source);
      if (Std.Unicode.Base.CodePoint._Is(_4_p)) {
        return Std.Unicode.Base.__default.IsInAssignedPlane(_4_p);
      }
      return false;
    }
  }
} // end of namespace Std.Unicode.Base
namespace Std.Unicode.Utf8EncodingForm {

  public partial class __default {
    public static bool IsMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<byte> s) {
      if ((new BigInteger((s).Count)) == (BigInteger.One)) {
        bool _0_b = Std.Unicode.Utf8EncodingForm.__default.IsWellFormedSingleCodeUnitSequence(s);
        return _0_b;
      } else if ((new BigInteger((s).Count)) == (new BigInteger(2))) {
        bool _1_b = Std.Unicode.Utf8EncodingForm.__default.IsWellFormedDoubleCodeUnitSequence(s);
        return _1_b;
      } else if ((new BigInteger((s).Count)) == (new BigInteger(3))) {
        bool _2_b = Std.Unicode.Utf8EncodingForm.__default.IsWellFormedTripleCodeUnitSequence(s);
        return _2_b;
      } else if ((new BigInteger((s).Count)) == (new BigInteger(4))) {
        bool _3_b = Std.Unicode.Utf8EncodingForm.__default.IsWellFormedQuadrupleCodeUnitSequence(s);
        return _3_b;
      } else {
        return false;
      }
    }
    public static bool IsWellFormedSingleCodeUnitSequence(Dafny.ISequence<byte> s) {
      byte _0_firstByte = (s).Select(BigInteger.Zero);
      return (true) && ((((byte)(0)) <= (_0_firstByte)) && ((_0_firstByte) <= ((byte)(127))));
    }
    public static bool IsWellFormedDoubleCodeUnitSequence(Dafny.ISequence<byte> s) {
      byte _0_firstByte = (s).Select(BigInteger.Zero);
      byte _1_secondByte = (s).Select(BigInteger.One);
      return ((((byte)(194)) <= (_0_firstByte)) && ((_0_firstByte) <= ((byte)(223)))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191))));
    }
    public static bool IsWellFormedTripleCodeUnitSequence(Dafny.ISequence<byte> s) {
      byte _0_firstByte = (s).Select(BigInteger.Zero);
      byte _1_secondByte = (s).Select(BigInteger.One);
      byte _2_thirdByte = (s).Select(new BigInteger(2));
      return ((((((_0_firstByte) == ((byte)(224))) && ((((byte)(160)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191))))) || (((((byte)(225)) <= (_0_firstByte)) && ((_0_firstByte) <= ((byte)(236)))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191)))))) || (((_0_firstByte) == ((byte)(237))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(159)))))) || (((((byte)(238)) <= (_0_firstByte)) && ((_0_firstByte) <= ((byte)(239)))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191)))))) && ((((byte)(128)) <= (_2_thirdByte)) && ((_2_thirdByte) <= ((byte)(191))));
    }
    public static bool IsWellFormedQuadrupleCodeUnitSequence(Dafny.ISequence<byte> s) {
      byte _0_firstByte = (s).Select(BigInteger.Zero);
      byte _1_secondByte = (s).Select(BigInteger.One);
      byte _2_thirdByte = (s).Select(new BigInteger(2));
      byte _3_fourthByte = (s).Select(new BigInteger(3));
      return ((((((_0_firstByte) == ((byte)(240))) && ((((byte)(144)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191))))) || (((((byte)(241)) <= (_0_firstByte)) && ((_0_firstByte) <= ((byte)(243)))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(191)))))) || (((_0_firstByte) == ((byte)(244))) && ((((byte)(128)) <= (_1_secondByte)) && ((_1_secondByte) <= ((byte)(143)))))) && ((((byte)(128)) <= (_2_thirdByte)) && ((_2_thirdByte) <= ((byte)(191))))) && ((((byte)(128)) <= (_3_fourthByte)) && ((_3_fourthByte) <= ((byte)(191))));
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<byte>> SplitPrefixMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<byte> s) {
      if (((new BigInteger((s).Count)) >= (BigInteger.One)) && (Std.Unicode.Utf8EncodingForm.__default.IsWellFormedSingleCodeUnitSequence((s).Take(BigInteger.One)))) {
        return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_Some((s).Take(BigInteger.One));
      } else if (((new BigInteger((s).Count)) >= (new BigInteger(2))) && (Std.Unicode.Utf8EncodingForm.__default.IsWellFormedDoubleCodeUnitSequence((s).Take(new BigInteger(2))))) {
        return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_Some((s).Take(new BigInteger(2)));
      } else if (((new BigInteger((s).Count)) >= (new BigInteger(3))) && (Std.Unicode.Utf8EncodingForm.__default.IsWellFormedTripleCodeUnitSequence((s).Take(new BigInteger(3))))) {
        return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_Some((s).Take(new BigInteger(3)));
      } else if (((new BigInteger((s).Count)) >= (new BigInteger(4))) && (Std.Unicode.Utf8EncodingForm.__default.IsWellFormedQuadrupleCodeUnitSequence((s).Take(new BigInteger(4))))) {
        return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_Some((s).Take(new BigInteger(4)));
      } else {
        return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_None();
      }
    }
    public static Dafny.ISequence<byte> EncodeScalarValue(uint v) {
      if ((v) <= (127U)) {
        return Std.Unicode.Utf8EncodingForm.__default.EncodeScalarValueSingleByte(v);
      } else if ((v) <= (2047U)) {
        return Std.Unicode.Utf8EncodingForm.__default.EncodeScalarValueDoubleByte(v);
      } else if ((v) <= (65535U)) {
        return Std.Unicode.Utf8EncodingForm.__default.EncodeScalarValueTripleByte(v);
      } else {
        return Std.Unicode.Utf8EncodingForm.__default.EncodeScalarValueQuadrupleByte(v);
      }
    }
    public static Dafny.ISequence<byte> EncodeScalarValueSingleByte(uint v) {
      byte _0_x = (byte)((v) & (127U));
      byte _1_firstByte = (byte)(_0_x);
      return Dafny.Sequence<byte>.FromElements(_1_firstByte);
    }
    public static Dafny.ISequence<byte> EncodeScalarValueDoubleByte(uint v) {
      byte _0_x = (byte)((v) & (63U));
      byte _1_y = (byte)(((v) & (1984U)) >> ((int)((byte)(6))));
      byte _2_firstByte = (byte)(((byte)(192)) | ((byte)(_1_y)));
      byte _3_secondByte = (byte)(((byte)(128)) | ((byte)(_0_x)));
      return Dafny.Sequence<byte>.FromElements(_2_firstByte, _3_secondByte);
    }
    public static Dafny.ISequence<byte> EncodeScalarValueTripleByte(uint v) {
      byte _0_x = (byte)((v) & (63U));
      byte _1_y = (byte)(((v) & (4032U)) >> ((int)((byte)(6))));
      byte _2_z = (byte)(((v) & (61440U)) >> ((int)((byte)(12))));
      byte _3_firstByte = (byte)(((byte)(224)) | ((byte)(_2_z)));
      byte _4_secondByte = (byte)(((byte)(128)) | ((byte)(_1_y)));
      byte _5_thirdByte = (byte)(((byte)(128)) | ((byte)(_0_x)));
      return Dafny.Sequence<byte>.FromElements(_3_firstByte, _4_secondByte, _5_thirdByte);
    }
    public static Dafny.ISequence<byte> EncodeScalarValueQuadrupleByte(uint v) {
      byte _0_x = (byte)((v) & (63U));
      byte _1_y = (byte)(((v) & (4032U)) >> ((int)((byte)(6))));
      byte _2_z = (byte)(((v) & (61440U)) >> ((int)((byte)(12))));
      byte _3_u2 = (byte)(((v) & (196608U)) >> ((int)((byte)(16))));
      byte _4_u1 = (byte)(((v) & (1835008U)) >> ((int)((byte)(18))));
      byte _5_firstByte = (byte)(((byte)(240)) | ((byte)(_4_u1)));
      byte _6_secondByte = (byte)(((byte)(((byte)(128)) | (unchecked((byte)(((byte)(((byte)(_3_u2)) << ((int)((byte)(4)))))))))) | ((byte)(_2_z)));
      byte _7_thirdByte = (byte)(((byte)(128)) | ((byte)(_1_y)));
      byte _8_fourthByte = (byte)(((byte)(128)) | ((byte)(_0_x)));
      return Dafny.Sequence<byte>.FromElements(_5_firstByte, _6_secondByte, _7_thirdByte, _8_fourthByte);
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<byte> m) {
      if ((new BigInteger((m).Count)) == (BigInteger.One)) {
        return Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceSingleByte(m);
      } else if ((new BigInteger((m).Count)) == (new BigInteger(2))) {
        return Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceDoubleByte(m);
      } else if ((new BigInteger((m).Count)) == (new BigInteger(3))) {
        return Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceTripleByte(m);
      } else {
        return Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceQuadrupleByte(m);
      }
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceSingleByte(Dafny.ISequence<byte> m) {
      byte _0_firstByte = (m).Select(BigInteger.Zero);
      byte _1_x = (byte)(_0_firstByte);
      return (uint)(_1_x);
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceDoubleByte(Dafny.ISequence<byte> m) {
      byte _0_firstByte = (m).Select(BigInteger.Zero);
      byte _1_secondByte = (m).Select(BigInteger.One);
      uint _2_y = (uint)((byte)((_0_firstByte) & ((byte)(31))));
      uint _3_x = (uint)((byte)((_1_secondByte) & ((byte)(63))));
      return (unchecked((uint)(((_2_y) << ((int)((byte)(6)))) & (uint)0xFFFFFFU))) | ((uint)(_3_x));
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceTripleByte(Dafny.ISequence<byte> m) {
      byte _0_firstByte = (m).Select(BigInteger.Zero);
      byte _1_secondByte = (m).Select(BigInteger.One);
      byte _2_thirdByte = (m).Select(new BigInteger(2));
      uint _3_z = (uint)((byte)((_0_firstByte) & ((byte)(15))));
      uint _4_y = (uint)((byte)((_1_secondByte) & ((byte)(63))));
      uint _5_x = (uint)((byte)((_2_thirdByte) & ((byte)(63))));
      return ((unchecked((uint)(((_3_z) << ((int)((byte)(12)))) & (uint)0xFFFFFFU))) | (unchecked((uint)(((_4_y) << ((int)((byte)(6)))) & (uint)0xFFFFFFU)))) | ((uint)(_5_x));
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceQuadrupleByte(Dafny.ISequence<byte> m) {
      byte _0_firstByte = (m).Select(BigInteger.Zero);
      byte _1_secondByte = (m).Select(BigInteger.One);
      byte _2_thirdByte = (m).Select(new BigInteger(2));
      byte _3_fourthByte = (m).Select(new BigInteger(3));
      uint _4_u1 = (uint)((byte)((_0_firstByte) & ((byte)(7))));
      uint _5_u2 = (uint)((byte)(((byte)((_1_secondByte) & ((byte)(48)))) >> ((int)((byte)(4)))));
      uint _6_z = (uint)((byte)((_1_secondByte) & ((byte)(15))));
      uint _7_y = (uint)((byte)((_2_thirdByte) & ((byte)(63))));
      uint _8_x = (uint)((byte)((_3_fourthByte) & ((byte)(63))));
      uint _9_r = ((((unchecked((uint)(((_4_u1) << ((int)((byte)(18)))) & (uint)0xFFFFFFU))) | (unchecked((uint)(((_5_u2) << ((int)((byte)(16)))) & (uint)0xFFFFFFU)))) | (unchecked((uint)(((_6_z) << ((int)((byte)(12)))) & (uint)0xFFFFFFU)))) | (unchecked((uint)(((_7_y) << ((int)((byte)(6)))) & (uint)0xFFFFFFU)))) | ((uint)(_8_x));
      return _9_r;
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<byte>>> PartitionCodeUnitSequenceChecked(Dafny.ISequence<byte> s)
    {
      Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<byte>>> maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<byte>>>.Default();
      if ((s).Equals(Dafny.Sequence<byte>.FromElements())) {
        maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<byte>>>.create_Some(Dafny.Sequence<Dafny.ISequence<byte>>.FromElements());
        return maybeParts;
      }
      Dafny.ISequence<Dafny.ISequence<byte>> _0_result;
      _0_result = Dafny.Sequence<Dafny.ISequence<byte>>.FromElements();
      Dafny.ISequence<byte> _1_rest;
      _1_rest = s;
      while ((new BigInteger((_1_rest).Count)).Sign == 1) {
        Std.Wrappers._IOption<Dafny.ISequence<byte>> _2_valueOrError0 = Std.Wrappers.Option<Dafny.ISequence<byte>>.Default();
        _2_valueOrError0 = Std.Unicode.Utf8EncodingForm.__default.SplitPrefixMinimalWellFormedCodeUnitSubsequence(_1_rest);
        if ((_2_valueOrError0).IsFailure()) {
          maybeParts = (_2_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.ISequence<byte>>>();
          return maybeParts;
        }
        Dafny.ISequence<byte> _3_prefix;
        _3_prefix = (_2_valueOrError0).Extract();
        _0_result = Dafny.Sequence<Dafny.ISequence<byte>>.Concat(_0_result, Dafny.Sequence<Dafny.ISequence<byte>>.FromElements(_3_prefix));
        _1_rest = (_1_rest).Drop(new BigInteger((_3_prefix).Count));
      }
      maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<byte>>>.create_Some(_0_result);
      return maybeParts;
      return maybeParts;
    }
    public static Dafny.ISequence<Dafny.ISequence<byte>> PartitionCodeUnitSequence(Dafny.ISequence<byte> s) {
      return (Std.Unicode.Utf8EncodingForm.__default.PartitionCodeUnitSequenceChecked(s)).Extract();
    }
    public static bool IsWellFormedCodeUnitSequence(Dafny.ISequence<byte> s) {
      return (Std.Unicode.Utf8EncodingForm.__default.PartitionCodeUnitSequenceChecked(s)).is_Some;
    }
    public static Dafny.ISequence<byte> EncodeScalarSequence(Dafny.ISequence<uint> vs)
    {
      Dafny.ISequence<byte> s = Std.Unicode.Utf8EncodingForm.WellFormedCodeUnitSeq.Default();
      s = Dafny.Sequence<byte>.FromElements();
      BigInteger _lo0 = BigInteger.Zero;
      for (BigInteger _0_i = new BigInteger((vs).Count); _lo0 < _0_i; ) {
        _0_i--;
        Dafny.ISequence<byte> _1_next;
        _1_next = Std.Unicode.Utf8EncodingForm.__default.EncodeScalarValue((vs).Select(_0_i));
        s = Dafny.Sequence<byte>.Concat(_1_next, s);
      }
      return s;
    }
    public static Dafny.ISequence<uint> DecodeCodeUnitSequence(Dafny.ISequence<byte> s) {
      Dafny.ISequence<Dafny.ISequence<byte>> _0_parts = Std.Unicode.Utf8EncodingForm.__default.PartitionCodeUnitSequence(s);
      Dafny.ISequence<uint> _1_vs = Std.Collections.Seq.__default.MapPartialFunction<Dafny.ISequence<byte>, uint>(Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequence, _0_parts);
      return _1_vs;
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<uint>> DecodeCodeUnitSequenceChecked(Dafny.ISequence<byte> s)
    {
      Std.Wrappers._IOption<Dafny.ISequence<uint>> maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.Default();
      Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<byte>>> _0_maybeParts;
      _0_maybeParts = Std.Unicode.Utf8EncodingForm.__default.PartitionCodeUnitSequenceChecked(s);
      if ((_0_maybeParts).is_None) {
        maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.create_None();
        return maybeVs;
      }
      Dafny.ISequence<Dafny.ISequence<byte>> _1_parts;
      _1_parts = (_0_maybeParts).dtor_value;
      Dafny.ISequence<uint> _2_vs;
      _2_vs = Std.Collections.Seq.__default.MapPartialFunction<Dafny.ISequence<byte>, uint>(Std.Unicode.Utf8EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequence, _1_parts);
      maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.create_Some(_2_vs);
      return maybeVs;
      return maybeVs;
    }
  }

  public partial class WellFormedCodeUnitSeq {
    private static readonly Dafny.ISequence<byte> Witness = Dafny.Sequence<byte>.FromElements();
    public static Dafny.ISequence<byte> Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<byte>>(Std.Unicode.Utf8EncodingForm.WellFormedCodeUnitSeq.Default());
    public static Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<byte> __source) {
      Dafny.ISequence<byte> _3_s = __source;
      return Std.Unicode.Utf8EncodingForm.__default.IsWellFormedCodeUnitSequence(_3_s);
    }
  }

  public partial class MinimalWellFormedCodeUnitSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<byte>>(Dafny.Sequence<byte>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<byte> __source) {
      Dafny.ISequence<byte> _4_s = __source;
      return Std.Unicode.Utf8EncodingForm.__default.IsMinimalWellFormedCodeUnitSubsequence(_4_s);
    }
  }
} // end of namespace Std.Unicode.Utf8EncodingForm
namespace Std.Unicode.Utf16EncodingForm {

  public partial class __default {
    public static bool IsMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<ushort> s) {
      if ((new BigInteger((s).Count)) == (BigInteger.One)) {
        return Std.Unicode.Utf16EncodingForm.__default.IsWellFormedSingleCodeUnitSequence(s);
      } else if ((new BigInteger((s).Count)) == (new BigInteger(2))) {
        bool _0_b = Std.Unicode.Utf16EncodingForm.__default.IsWellFormedDoubleCodeUnitSequence(s);
        return _0_b;
      } else {
        return false;
      }
    }
    public static bool IsWellFormedSingleCodeUnitSequence(Dafny.ISequence<ushort> s) {
      ushort _0_firstWord = (s).Select(BigInteger.Zero);
      return ((((ushort)(0)) <= (_0_firstWord)) && ((_0_firstWord) <= ((ushort)(55295)))) || ((((ushort)(57344)) <= (_0_firstWord)) && ((_0_firstWord) <= ((ushort)(65535))));
    }
    public static bool IsWellFormedDoubleCodeUnitSequence(Dafny.ISequence<ushort> s) {
      ushort _0_firstWord = (s).Select(BigInteger.Zero);
      ushort _1_secondWord = (s).Select(BigInteger.One);
      return ((((ushort)(55296)) <= (_0_firstWord)) && ((_0_firstWord) <= ((ushort)(56319)))) && ((((ushort)(56320)) <= (_1_secondWord)) && ((_1_secondWord) <= ((ushort)(57343))));
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<ushort>> SplitPrefixMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<ushort> s) {
      if (((new BigInteger((s).Count)) >= (BigInteger.One)) && (Std.Unicode.Utf16EncodingForm.__default.IsWellFormedSingleCodeUnitSequence((s).Take(BigInteger.One)))) {
        return Std.Wrappers.Option<Dafny.ISequence<ushort>>.create_Some((s).Take(BigInteger.One));
      } else if (((new BigInteger((s).Count)) >= (new BigInteger(2))) && (Std.Unicode.Utf16EncodingForm.__default.IsWellFormedDoubleCodeUnitSequence((s).Take(new BigInteger(2))))) {
        return Std.Wrappers.Option<Dafny.ISequence<ushort>>.create_Some((s).Take(new BigInteger(2)));
      } else {
        return Std.Wrappers.Option<Dafny.ISequence<ushort>>.create_None();
      }
    }
    public static Dafny.ISequence<ushort> EncodeScalarValue(uint v) {
      if ((((0U) <= (v)) && ((v) <= (55295U))) || (((57344U) <= (v)) && ((v) <= (65535U)))) {
        return Std.Unicode.Utf16EncodingForm.__default.EncodeScalarValueSingleWord(v);
      } else {
        return Std.Unicode.Utf16EncodingForm.__default.EncodeScalarValueDoubleWord(v);
      }
    }
    public static Dafny.ISequence<ushort> EncodeScalarValueSingleWord(uint v) {
      ushort _0_firstWord = (ushort)(v);
      return Dafny.Sequence<ushort>.FromElements(_0_firstWord);
    }
    public static Dafny.ISequence<ushort> EncodeScalarValueDoubleWord(uint v) {
      ushort _0_x2 = (ushort)((v) & (1023U));
      byte _1_x1 = (byte)(((v) & (64512U)) >> ((int)((byte)(10))));
      byte _2_u = (byte)(((v) & (2031616U)) >> ((int)((byte)(16))));
      byte _3_w = (byte)(unchecked((byte)(((byte)((_2_u) - ((byte)(1)))) & (byte)0x1F)));
      ushort _4_firstWord = (ushort)(((ushort)(((ushort)(55296)) | (unchecked((ushort)(((ushort)(((ushort)(_3_w)) << ((int)((byte)(6)))))))))) | ((ushort)(_1_x1)));
      ushort _5_secondWord = (ushort)(((ushort)(56320)) | ((ushort)(_0_x2)));
      return Dafny.Sequence<ushort>.FromElements(_4_firstWord, _5_secondWord);
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequence(Dafny.ISequence<ushort> m) {
      if ((new BigInteger((m).Count)) == (BigInteger.One)) {
        return Std.Unicode.Utf16EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceSingleWord(m);
      } else {
        return Std.Unicode.Utf16EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequenceDoubleWord(m);
      }
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceSingleWord(Dafny.ISequence<ushort> m) {
      ushort _0_firstWord = (m).Select(BigInteger.Zero);
      ushort _1_x = _0_firstWord;
      return (uint)(_1_x);
    }
    public static uint DecodeMinimalWellFormedCodeUnitSubsequenceDoubleWord(Dafny.ISequence<ushort> m) {
      ushort _0_firstWord = (m).Select(BigInteger.Zero);
      ushort _1_secondWord = (m).Select(BigInteger.One);
      uint _2_x2 = (uint)((ushort)((_1_secondWord) & ((ushort)(1023))));
      uint _3_x1 = (uint)((ushort)((_0_firstWord) & ((ushort)(63))));
      uint _4_w = (uint)((ushort)(((ushort)((_0_firstWord) & ((ushort)(960)))) >> ((int)((byte)(6)))));
      uint _5_u = unchecked((uint)(((_4_w) + (1U)) & (uint)0xFFFFFFU));
      uint _6_v = ((unchecked((uint)(((_5_u) << ((int)((byte)(16)))) & (uint)0xFFFFFFU))) | (unchecked((uint)(((_3_x1) << ((int)((byte)(10)))) & (uint)0xFFFFFFU)))) | ((uint)(_2_x2));
      return _6_v;
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<ushort>>> PartitionCodeUnitSequenceChecked(Dafny.ISequence<ushort> s)
    {
      Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<ushort>>> maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<ushort>>>.Default();
      if ((s).Equals(Dafny.Sequence<ushort>.FromElements())) {
        maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<ushort>>>.create_Some(Dafny.Sequence<Dafny.ISequence<ushort>>.FromElements());
        return maybeParts;
      }
      Dafny.ISequence<Dafny.ISequence<ushort>> _0_result;
      _0_result = Dafny.Sequence<Dafny.ISequence<ushort>>.FromElements();
      Dafny.ISequence<ushort> _1_rest;
      _1_rest = s;
      while ((new BigInteger((_1_rest).Count)).Sign == 1) {
        Std.Wrappers._IOption<Dafny.ISequence<ushort>> _2_valueOrError0 = Std.Wrappers.Option<Dafny.ISequence<ushort>>.Default();
        _2_valueOrError0 = Std.Unicode.Utf16EncodingForm.__default.SplitPrefixMinimalWellFormedCodeUnitSubsequence(_1_rest);
        if ((_2_valueOrError0).IsFailure()) {
          maybeParts = (_2_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.ISequence<ushort>>>();
          return maybeParts;
        }
        Dafny.ISequence<ushort> _3_prefix;
        _3_prefix = (_2_valueOrError0).Extract();
        _0_result = Dafny.Sequence<Dafny.ISequence<ushort>>.Concat(_0_result, Dafny.Sequence<Dafny.ISequence<ushort>>.FromElements(_3_prefix));
        _1_rest = (_1_rest).Drop(new BigInteger((_3_prefix).Count));
      }
      maybeParts = Std.Wrappers.Option<Dafny.ISequence<Dafny.ISequence<ushort>>>.create_Some(_0_result);
      return maybeParts;
      return maybeParts;
    }
    public static Dafny.ISequence<Dafny.ISequence<ushort>> PartitionCodeUnitSequence(Dafny.ISequence<ushort> s) {
      return (Std.Unicode.Utf16EncodingForm.__default.PartitionCodeUnitSequenceChecked(s)).Extract();
    }
    public static bool IsWellFormedCodeUnitSequence(Dafny.ISequence<ushort> s) {
      return (Std.Unicode.Utf16EncodingForm.__default.PartitionCodeUnitSequenceChecked(s)).is_Some;
    }
    public static Dafny.ISequence<ushort> EncodeScalarSequence(Dafny.ISequence<uint> vs)
    {
      Dafny.ISequence<ushort> s = Std.Unicode.Utf16EncodingForm.WellFormedCodeUnitSeq.Default();
      s = Dafny.Sequence<ushort>.FromElements();
      BigInteger _lo0 = BigInteger.Zero;
      for (BigInteger _0_i = new BigInteger((vs).Count); _lo0 < _0_i; ) {
        _0_i--;
        Dafny.ISequence<ushort> _1_next;
        _1_next = Std.Unicode.Utf16EncodingForm.__default.EncodeScalarValue((vs).Select(_0_i));
        s = Dafny.Sequence<ushort>.Concat(_1_next, s);
      }
      return s;
    }
    public static Dafny.ISequence<uint> DecodeCodeUnitSequence(Dafny.ISequence<ushort> s) {
      Dafny.ISequence<Dafny.ISequence<ushort>> _0_parts = Std.Unicode.Utf16EncodingForm.__default.PartitionCodeUnitSequence(s);
      Dafny.ISequence<uint> _1_vs = Std.Collections.Seq.__default.MapPartialFunction<Dafny.ISequence<ushort>, uint>(Std.Unicode.Utf16EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequence, _0_parts);
      return _1_vs;
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<uint>> DecodeCodeUnitSequenceChecked(Dafny.ISequence<ushort> s)
    {
      Std.Wrappers._IOption<Dafny.ISequence<uint>> maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.Default();
      Std.Wrappers._IOption<Dafny.ISequence<Dafny.ISequence<ushort>>> _0_maybeParts;
      _0_maybeParts = Std.Unicode.Utf16EncodingForm.__default.PartitionCodeUnitSequenceChecked(s);
      if ((_0_maybeParts).is_None) {
        maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.create_None();
        return maybeVs;
      }
      Dafny.ISequence<Dafny.ISequence<ushort>> _1_parts;
      _1_parts = (_0_maybeParts).dtor_value;
      Dafny.ISequence<uint> _2_vs;
      _2_vs = Std.Collections.Seq.__default.MapPartialFunction<Dafny.ISequence<ushort>, uint>(Std.Unicode.Utf16EncodingForm.__default.DecodeMinimalWellFormedCodeUnitSubsequence, _1_parts);
      maybeVs = Std.Wrappers.Option<Dafny.ISequence<uint>>.create_Some(_2_vs);
      return maybeVs;
      return maybeVs;
    }
  }

  public partial class WellFormedCodeUnitSeq {
    private static readonly Dafny.ISequence<ushort> Witness = Dafny.Sequence<ushort>.FromElements();
    public static Dafny.ISequence<ushort> Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<ushort>>(Std.Unicode.Utf16EncodingForm.WellFormedCodeUnitSeq.Default());
    public static Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<ushort> __source) {
      Dafny.ISequence<ushort> _3_s = __source;
      return Std.Unicode.Utf16EncodingForm.__default.IsWellFormedCodeUnitSequence(_3_s);
    }
  }

  public partial class MinimalWellFormedCodeUnitSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<ushort>>(Dafny.Sequence<ushort>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<ushort> __source) {
      Dafny.ISequence<ushort> _4_s = __source;
      return Std.Unicode.Utf16EncodingForm.__default.IsMinimalWellFormedCodeUnitSubsequence(_4_s);
    }
  }
} // end of namespace Std.Unicode.Utf16EncodingForm
namespace Std.Unicode.UnicodeStringsWithUnicodeChar {

  public partial class __default {
    public static uint CharAsUnicodeScalarValue(Dafny.Rune c) {
      return (uint)(new BigInteger((c).Value));
    }
    public static Dafny.Rune CharFromUnicodeScalarValue(uint sv) {
      return new Dafny.Rune((int)(new BigInteger(sv)));
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<byte>> ToUTF8Checked(Dafny.ISequence<Dafny.Rune> s) {
      Dafny.ISequence<uint> _0_asCodeUnits = Std.Collections.Seq.__default.Map<Dafny.Rune, uint>(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.CharAsUnicodeScalarValue, s);
      Dafny.ISequence<byte> _1_asUtf8CodeUnits = Std.Unicode.Utf8EncodingForm.__default.EncodeScalarSequence(_0_asCodeUnits);
      Dafny.ISequence<byte> _2_asBytes = Std.Collections.Seq.__default.Map<byte, byte>(((System.Func<byte, byte>)((_3_cu) => {
        return (byte)(_3_cu);
      })), _1_asUtf8CodeUnits);
      return Std.Wrappers.Option<Dafny.ISequence<byte>>.create_Some(_2_asBytes);
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> FromUTF8Checked(Dafny.ISequence<byte> bs) {
      Dafny.ISequence<byte> _0_asCodeUnits = Std.Collections.Seq.__default.Map<byte, byte>(((System.Func<byte, byte>)((_1_c) => {
        return (byte)(_1_c);
      })), bs);
      Std.Wrappers._IOption<Dafny.ISequence<uint>> _2_valueOrError0 = Std.Unicode.Utf8EncodingForm.__default.DecodeCodeUnitSequenceChecked(_0_asCodeUnits);
      if ((_2_valueOrError0).IsFailure()) {
        return (_2_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
      } else {
        Dafny.ISequence<uint> _3_utf32 = (_2_valueOrError0).Extract();
        Dafny.ISequence<Dafny.Rune> _4_asChars = Std.Collections.Seq.__default.Map<uint, Dafny.Rune>(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.CharFromUnicodeScalarValue, _3_utf32);
        return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(_4_asChars);
      }
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<ushort>> ToUTF16Checked(Dafny.ISequence<Dafny.Rune> s) {
      Dafny.ISequence<uint> _0_asCodeUnits = Std.Collections.Seq.__default.Map<Dafny.Rune, uint>(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.CharAsUnicodeScalarValue, s);
      Dafny.ISequence<ushort> _1_asUtf16CodeUnits = Std.Unicode.Utf16EncodingForm.__default.EncodeScalarSequence(_0_asCodeUnits);
      Dafny.ISequence<ushort> _2_asBytes = Std.Collections.Seq.__default.Map<ushort, ushort>(((System.Func<ushort, ushort>)((_3_cu) => {
        return (ushort)(_3_cu);
      })), _1_asUtf16CodeUnits);
      return Std.Wrappers.Option<Dafny.ISequence<ushort>>.create_Some(_2_asBytes);
    }
    public static Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>> FromUTF16Checked(Dafny.ISequence<ushort> bs) {
      Dafny.ISequence<ushort> _0_asCodeUnits = Std.Collections.Seq.__default.Map<ushort, ushort>(((System.Func<ushort, ushort>)((_1_c) => {
        return (ushort)(_1_c);
      })), bs);
      Std.Wrappers._IOption<Dafny.ISequence<uint>> _2_valueOrError0 = Std.Unicode.Utf16EncodingForm.__default.DecodeCodeUnitSequenceChecked(_0_asCodeUnits);
      if ((_2_valueOrError0).IsFailure()) {
        return (_2_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
      } else {
        Dafny.ISequence<uint> _3_utf32 = (_2_valueOrError0).Extract();
        Dafny.ISequence<Dafny.Rune> _4_asChars = Std.Collections.Seq.__default.Map<uint, Dafny.Rune>(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.CharFromUnicodeScalarValue, _3_utf32);
        return Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(_4_asChars);
      }
    }
    public static Dafny.ISequence<byte> ASCIIToUTF8(Dafny.ISequence<Dafny.Rune> s) {
      return Std.Collections.Seq.__default.Map<Dafny.Rune, byte>(((System.Func<Dafny.Rune, byte>)((_0_c) => {
        return (byte)((_0_c).Value);
      })), s);
    }
    public static Dafny.ISequence<ushort> ASCIIToUTF16(Dafny.ISequence<Dafny.Rune> s) {
      return Std.Collections.Seq.__default.Map<Dafny.Rune, ushort>(((System.Func<Dafny.Rune, ushort>)((_0_c) => {
        return (ushort)((_0_c).Value);
      })), s);
    }
  }
} // end of namespace Std.Unicode.UnicodeStringsWithUnicodeChar
namespace Std.Unicode.Utf8EncodingScheme {

  public partial class __default {
    public static Dafny.ISequence<byte> Serialize(Dafny.ISequence<byte> s) {
      return Std.Collections.Seq.__default.Map<byte, byte>(((System.Func<byte, byte>)((_0_c) => {
        return (byte)(_0_c);
      })), s);
    }
    public static Dafny.ISequence<byte> Deserialize(Dafny.ISequence<byte> b) {
      return Std.Collections.Seq.__default.Map<byte, byte>(((System.Func<byte, byte>)((_0_b) => {
        return (byte)(_0_b);
      })), b);
    }
  }
} // end of namespace Std.Unicode.Utf8EncodingScheme
namespace Std.Unicode {

} // end of namespace Std.Unicode
namespace Std.JSON.Values {

  public partial class __default {
    public static Std.JSON.Values._IDecimal Int(BigInteger n) {
      return Std.JSON.Values.Decimal.create(n, BigInteger.Zero);
    }
  }

  public interface _IDecimal {
    bool is_Decimal { get; }
    BigInteger dtor_n { get; }
    BigInteger dtor_e10 { get; }
    _IDecimal DowncastClone();
  }
  public class Decimal : _IDecimal {
    public readonly BigInteger _n;
    public readonly BigInteger _e10;
    public Decimal(BigInteger n, BigInteger e10) {
      this._n = n;
      this._e10 = e10;
    }
    public _IDecimal DowncastClone() {
      if (this is _IDecimal dt) { return dt; }
      return new Decimal(_n, _e10);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.Decimal;
      return oth != null && this._n == oth._n && this._e10 == oth._e10;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._n));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._e10));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.Decimal.Decimal";
      s += "(";
      s += Dafny.Helpers.ToString(this._n);
      s += ", ";
      s += Dafny.Helpers.ToString(this._e10);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Values._IDecimal theDefault = create(BigInteger.Zero, BigInteger.Zero);
    public static Std.JSON.Values._IDecimal Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Values._IDecimal> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Values._IDecimal>(Std.JSON.Values.Decimal.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Values._IDecimal> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IDecimal create(BigInteger n, BigInteger e10) {
      return new Decimal(n, e10);
    }
    public static _IDecimal create_Decimal(BigInteger n, BigInteger e10) {
      return create(n, e10);
    }
    public bool is_Decimal { get { return true; } }
    public BigInteger dtor_n {
      get {
        return this._n;
      }
    }
    public BigInteger dtor_e10 {
      get {
        return this._e10;
      }
    }
  }

  public interface _IJSON {
    bool is_Null { get; }
    bool is_Bool { get; }
    bool is_String { get; }
    bool is_Number { get; }
    bool is_Object { get; }
    bool is_Array { get; }
    bool dtor_b { get; }
    Dafny.ISequence<Dafny.Rune> dtor_str { get; }
    Std.JSON.Values._IDecimal dtor_num { get; }
    Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> dtor_obj { get; }
    Dafny.ISequence<Std.JSON.Values._IJSON> dtor_arr { get; }
    _IJSON DowncastClone();
  }
  public abstract class JSON : _IJSON {
    public JSON() {
    }
    private static readonly Std.JSON.Values._IJSON theDefault = create_Null();
    public static Std.JSON.Values._IJSON Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Values._IJSON> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Values._IJSON>(Std.JSON.Values.JSON.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Values._IJSON> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IJSON create_Null() {
      return new JSON_Null();
    }
    public static _IJSON create_Bool(bool b) {
      return new JSON_Bool(b);
    }
    public static _IJSON create_String(Dafny.ISequence<Dafny.Rune> str) {
      return new JSON_String(str);
    }
    public static _IJSON create_Number(Std.JSON.Values._IDecimal num) {
      return new JSON_Number(num);
    }
    public static _IJSON create_Object(Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> obj) {
      return new JSON_Object(obj);
    }
    public static _IJSON create_Array(Dafny.ISequence<Std.JSON.Values._IJSON> arr) {
      return new JSON_Array(arr);
    }
    public bool is_Null { get { return this is JSON_Null; } }
    public bool is_Bool { get { return this is JSON_Bool; } }
    public bool is_String { get { return this is JSON_String; } }
    public bool is_Number { get { return this is JSON_Number; } }
    public bool is_Object { get { return this is JSON_Object; } }
    public bool is_Array { get { return this is JSON_Array; } }
    public bool dtor_b {
      get {
        var d = this;
        return ((JSON_Bool)d)._b;
      }
    }
    public Dafny.ISequence<Dafny.Rune> dtor_str {
      get {
        var d = this;
        return ((JSON_String)d)._str;
      }
    }
    public Std.JSON.Values._IDecimal dtor_num {
      get {
        var d = this;
        return ((JSON_Number)d)._num;
      }
    }
    public Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> dtor_obj {
      get {
        var d = this;
        return ((JSON_Object)d)._obj;
      }
    }
    public Dafny.ISequence<Std.JSON.Values._IJSON> dtor_arr {
      get {
        var d = this;
        return ((JSON_Array)d)._arr;
      }
    }
    public abstract _IJSON DowncastClone();
  }
  public class JSON_Null : JSON {
    public JSON_Null() : base() {
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_Null();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_Null;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.Null";
      return s;
    }
  }
  public class JSON_Bool : JSON {
    public readonly bool _b;
    public JSON_Bool(bool b) : base() {
      this._b = b;
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_Bool(_b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_Bool;
      return oth != null && this._b == oth._b;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.Bool";
      s += "(";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class JSON_String : JSON {
    public readonly Dafny.ISequence<Dafny.Rune> _str;
    public JSON_String(Dafny.ISequence<Dafny.Rune> str) : base() {
      this._str = str;
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_String(_str);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_String;
      return oth != null && object.Equals(this._str, oth._str);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._str));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.String";
      s += "(";
      s += this._str.ToVerbatimString(true);
      s += ")";
      return s;
    }
  }
  public class JSON_Number : JSON {
    public readonly Std.JSON.Values._IDecimal _num;
    public JSON_Number(Std.JSON.Values._IDecimal num) : base() {
      this._num = num;
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_Number(_num);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_Number;
      return oth != null && object.Equals(this._num, oth._num);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 3;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._num));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.Number";
      s += "(";
      s += Dafny.Helpers.ToString(this._num);
      s += ")";
      return s;
    }
  }
  public class JSON_Object : JSON {
    public readonly Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> _obj;
    public JSON_Object(Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> obj) : base() {
      this._obj = obj;
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_Object(_obj);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_Object;
      return oth != null && object.Equals(this._obj, oth._obj);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 4;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._obj));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.Object";
      s += "(";
      s += Dafny.Helpers.ToString(this._obj);
      s += ")";
      return s;
    }
  }
  public class JSON_Array : JSON {
    public readonly Dafny.ISequence<Std.JSON.Values._IJSON> _arr;
    public JSON_Array(Dafny.ISequence<Std.JSON.Values._IJSON> arr) : base() {
      this._arr = arr;
    }
    public override _IJSON DowncastClone() {
      if (this is _IJSON dt) { return dt; }
      return new JSON_Array(_arr);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Values.JSON_Array;
      return oth != null && object.Equals(this._arr, oth._arr);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 5;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._arr));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Values.JSON.Array";
      s += "(";
      s += Dafny.Helpers.ToString(this._arr);
      s += ")";
      return s;
    }
  }
} // end of namespace Std.JSON.Values
namespace Std.JSON.Errors {


  public interface _IDeserializationError {
    bool is_UnterminatedSequence { get; }
    bool is_UnsupportedEscape { get; }
    bool is_EscapeAtEOS { get; }
    bool is_EmptyNumber { get; }
    bool is_ExpectingEOF { get; }
    bool is_IntOverflow { get; }
    bool is_ReachedEOF { get; }
    bool is_ExpectingByte { get; }
    bool is_ExpectingAnyByte { get; }
    bool is_InvalidUnicode { get; }
    Dafny.ISequence<Dafny.Rune> dtor_str { get; }
    byte dtor_expected { get; }
    short dtor_b { get; }
    Dafny.ISequence<byte> dtor_expected__sq { get; }
    _IDeserializationError DowncastClone();
    Dafny.ISequence<Dafny.Rune> _ToString();
  }
  public abstract class DeserializationError : _IDeserializationError {
    public DeserializationError() {
    }
    private static readonly Std.JSON.Errors._IDeserializationError theDefault = create_UnterminatedSequence();
    public static Std.JSON.Errors._IDeserializationError Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Errors._IDeserializationError> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Errors._IDeserializationError>(Std.JSON.Errors.DeserializationError.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Errors._IDeserializationError> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IDeserializationError create_UnterminatedSequence() {
      return new DeserializationError_UnterminatedSequence();
    }
    public static _IDeserializationError create_UnsupportedEscape(Dafny.ISequence<Dafny.Rune> str) {
      return new DeserializationError_UnsupportedEscape(str);
    }
    public static _IDeserializationError create_EscapeAtEOS() {
      return new DeserializationError_EscapeAtEOS();
    }
    public static _IDeserializationError create_EmptyNumber() {
      return new DeserializationError_EmptyNumber();
    }
    public static _IDeserializationError create_ExpectingEOF() {
      return new DeserializationError_ExpectingEOF();
    }
    public static _IDeserializationError create_IntOverflow() {
      return new DeserializationError_IntOverflow();
    }
    public static _IDeserializationError create_ReachedEOF() {
      return new DeserializationError_ReachedEOF();
    }
    public static _IDeserializationError create_ExpectingByte(byte expected, short b) {
      return new DeserializationError_ExpectingByte(expected, b);
    }
    public static _IDeserializationError create_ExpectingAnyByte(Dafny.ISequence<byte> expected__sq, short b) {
      return new DeserializationError_ExpectingAnyByte(expected__sq, b);
    }
    public static _IDeserializationError create_InvalidUnicode() {
      return new DeserializationError_InvalidUnicode();
    }
    public bool is_UnterminatedSequence { get { return this is DeserializationError_UnterminatedSequence; } }
    public bool is_UnsupportedEscape { get { return this is DeserializationError_UnsupportedEscape; } }
    public bool is_EscapeAtEOS { get { return this is DeserializationError_EscapeAtEOS; } }
    public bool is_EmptyNumber { get { return this is DeserializationError_EmptyNumber; } }
    public bool is_ExpectingEOF { get { return this is DeserializationError_ExpectingEOF; } }
    public bool is_IntOverflow { get { return this is DeserializationError_IntOverflow; } }
    public bool is_ReachedEOF { get { return this is DeserializationError_ReachedEOF; } }
    public bool is_ExpectingByte { get { return this is DeserializationError_ExpectingByte; } }
    public bool is_ExpectingAnyByte { get { return this is DeserializationError_ExpectingAnyByte; } }
    public bool is_InvalidUnicode { get { return this is DeserializationError_InvalidUnicode; } }
    public Dafny.ISequence<Dafny.Rune> dtor_str {
      get {
        var d = this;
        return ((DeserializationError_UnsupportedEscape)d)._str;
      }
    }
    public byte dtor_expected {
      get {
        var d = this;
        return ((DeserializationError_ExpectingByte)d)._expected;
      }
    }
    public short dtor_b {
      get {
        var d = this;
        if (d is DeserializationError_ExpectingByte) { return ((DeserializationError_ExpectingByte)d)._b; }
        return ((DeserializationError_ExpectingAnyByte)d)._b;
      }
    }
    public Dafny.ISequence<byte> dtor_expected__sq {
      get {
        var d = this;
        return ((DeserializationError_ExpectingAnyByte)d)._expected__sq;
      }
    }
    public abstract _IDeserializationError DowncastClone();
    public Dafny.ISequence<Dafny.Rune> _ToString() {
      Std.JSON.Errors._IDeserializationError _source0 = this;
      {
        if (_source0.is_UnterminatedSequence) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Unterminated sequence");
        }
      }
      {
        if (_source0.is_UnsupportedEscape) {
          Dafny.ISequence<Dafny.Rune> _0_str = _source0.dtor_str;
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Unsupported escape sequence: "), _0_str);
        }
      }
      {
        if (_source0.is_EscapeAtEOS) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Escape character at end of string");
        }
      }
      {
        if (_source0.is_EmptyNumber) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Number must contain at least one digit");
        }
      }
      {
        if (_source0.is_ExpectingEOF) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Expecting EOF");
        }
      }
      {
        if (_source0.is_IntOverflow) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Input length does not fit in a 32-bit counter");
        }
      }
      {
        if (_source0.is_ReachedEOF) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Reached EOF");
        }
      }
      {
        if (_source0.is_ExpectingByte) {
          byte _1_b0 = _source0.dtor_expected;
          short _2_b = _source0.dtor_b;
          Dafny.ISequence<Dafny.Rune> _3_c = (((_2_b) > ((short)(0))) ? (Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_2_b)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"))) : (Dafny.Sequence<Dafny.Rune>.UnicodeFromString("EOF")));
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Expecting '"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_1_b0)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("', read ")), _3_c);
        }
      }
      {
        if (_source0.is_ExpectingAnyByte) {
          Dafny.ISequence<byte> _4_bs0 = _source0.dtor_expected__sq;
          short _5_b = _source0.dtor_b;
          Dafny.ISequence<Dafny.Rune> _6_c = (((_5_b) > ((short)(0))) ? (Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_5_b)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"))) : (Dafny.Sequence<Dafny.Rune>.UnicodeFromString("EOF")));
          Dafny.ISequence<Dafny.Rune> _7_c0s = ((System.Func<Dafny.ISequence<Dafny.Rune>>) (() => {
            BigInteger dim4 = new BigInteger((_4_bs0).Count);
            var arr4 = new Dafny.Rune[Dafny.Helpers.ToIntChecked(dim4, "array size exceeds memory limit")];
            for (int i4 = 0; i4 < dim4; i4++) {
              var _8_idx = (BigInteger) i4;
              arr4[(int)(_8_idx)] = new Dafny.Rune((int)((_4_bs0).Select(_8_idx)));
            }
            return Dafny.Sequence<Dafny.Rune>.FromArray(arr4);
          }))();
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Expecting one of '"), _7_c0s), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("', read ")), _6_c);
        }
      }
      {
        return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Invalid Unicode sequence");
      }
    }
  }
  public class DeserializationError_UnterminatedSequence : DeserializationError {
    public DeserializationError_UnterminatedSequence() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_UnterminatedSequence();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_UnterminatedSequence;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.UnterminatedSequence";
      return s;
    }
  }
  public class DeserializationError_UnsupportedEscape : DeserializationError {
    public readonly Dafny.ISequence<Dafny.Rune> _str;
    public DeserializationError_UnsupportedEscape(Dafny.ISequence<Dafny.Rune> str) : base() {
      this._str = str;
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_UnsupportedEscape(_str);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_UnsupportedEscape;
      return oth != null && object.Equals(this._str, oth._str);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._str));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.UnsupportedEscape";
      s += "(";
      s += this._str.ToVerbatimString(true);
      s += ")";
      return s;
    }
  }
  public class DeserializationError_EscapeAtEOS : DeserializationError {
    public DeserializationError_EscapeAtEOS() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_EscapeAtEOS();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_EscapeAtEOS;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.EscapeAtEOS";
      return s;
    }
  }
  public class DeserializationError_EmptyNumber : DeserializationError {
    public DeserializationError_EmptyNumber() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_EmptyNumber();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_EmptyNumber;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 3;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.EmptyNumber";
      return s;
    }
  }
  public class DeserializationError_ExpectingEOF : DeserializationError {
    public DeserializationError_ExpectingEOF() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_ExpectingEOF();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_ExpectingEOF;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 4;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.ExpectingEOF";
      return s;
    }
  }
  public class DeserializationError_IntOverflow : DeserializationError {
    public DeserializationError_IntOverflow() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_IntOverflow();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_IntOverflow;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 5;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.IntOverflow";
      return s;
    }
  }
  public class DeserializationError_ReachedEOF : DeserializationError {
    public DeserializationError_ReachedEOF() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_ReachedEOF();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_ReachedEOF;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 6;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.ReachedEOF";
      return s;
    }
  }
  public class DeserializationError_ExpectingByte : DeserializationError {
    public readonly byte _expected;
    public readonly short _b;
    public DeserializationError_ExpectingByte(byte expected, short b) : base() {
      this._expected = expected;
      this._b = b;
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_ExpectingByte(_expected, _b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_ExpectingByte;
      return oth != null && this._expected == oth._expected && this._b == oth._b;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 7;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._expected));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.ExpectingByte";
      s += "(";
      s += Dafny.Helpers.ToString(this._expected);
      s += ", ";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class DeserializationError_ExpectingAnyByte : DeserializationError {
    public readonly Dafny.ISequence<byte> _expected__sq;
    public readonly short _b;
    public DeserializationError_ExpectingAnyByte(Dafny.ISequence<byte> expected__sq, short b) : base() {
      this._expected__sq = expected__sq;
      this._b = b;
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_ExpectingAnyByte(_expected__sq, _b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_ExpectingAnyByte;
      return oth != null && object.Equals(this._expected__sq, oth._expected__sq) && this._b == oth._b;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 8;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._expected__sq));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.ExpectingAnyByte";
      s += "(";
      s += Dafny.Helpers.ToString(this._expected__sq);
      s += ", ";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class DeserializationError_InvalidUnicode : DeserializationError {
    public DeserializationError_InvalidUnicode() : base() {
    }
    public override _IDeserializationError DowncastClone() {
      if (this is _IDeserializationError dt) { return dt; }
      return new DeserializationError_InvalidUnicode();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.DeserializationError_InvalidUnicode;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 9;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.DeserializationError.InvalidUnicode";
      return s;
    }
  }

  public interface _ISerializationError {
    bool is_OutOfMemory { get; }
    bool is_IntTooLarge { get; }
    bool is_StringTooLong { get; }
    bool is_InvalidUnicode { get; }
    BigInteger dtor_i { get; }
    Dafny.ISequence<Dafny.Rune> dtor_s { get; }
    _ISerializationError DowncastClone();
    Dafny.ISequence<Dafny.Rune> _ToString();
  }
  public abstract class SerializationError : _ISerializationError {
    public SerializationError() {
    }
    private static readonly Std.JSON.Errors._ISerializationError theDefault = create_OutOfMemory();
    public static Std.JSON.Errors._ISerializationError Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Errors._ISerializationError> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Errors._ISerializationError>(Std.JSON.Errors.SerializationError.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Errors._ISerializationError> _TypeDescriptor() {
      return _TYPE;
    }
    public static _ISerializationError create_OutOfMemory() {
      return new SerializationError_OutOfMemory();
    }
    public static _ISerializationError create_IntTooLarge(BigInteger i) {
      return new SerializationError_IntTooLarge(i);
    }
    public static _ISerializationError create_StringTooLong(Dafny.ISequence<Dafny.Rune> s) {
      return new SerializationError_StringTooLong(s);
    }
    public static _ISerializationError create_InvalidUnicode() {
      return new SerializationError_InvalidUnicode();
    }
    public bool is_OutOfMemory { get { return this is SerializationError_OutOfMemory; } }
    public bool is_IntTooLarge { get { return this is SerializationError_IntTooLarge; } }
    public bool is_StringTooLong { get { return this is SerializationError_StringTooLong; } }
    public bool is_InvalidUnicode { get { return this is SerializationError_InvalidUnicode; } }
    public BigInteger dtor_i {
      get {
        var d = this;
        return ((SerializationError_IntTooLarge)d)._i;
      }
    }
    public Dafny.ISequence<Dafny.Rune> dtor_s {
      get {
        var d = this;
        return ((SerializationError_StringTooLong)d)._s;
      }
    }
    public abstract _ISerializationError DowncastClone();
    public Dafny.ISequence<Dafny.Rune> _ToString() {
      Std.JSON.Errors._ISerializationError _source0 = this;
      {
        if (_source0.is_OutOfMemory) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Out of memory");
        }
      }
      {
        if (_source0.is_IntTooLarge) {
          BigInteger _0_i = _source0.dtor_i;
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Integer too large: "), Std.Strings.__default.OfInt(_0_i));
        }
      }
      {
        if (_source0.is_StringTooLong) {
          Dafny.ISequence<Dafny.Rune> _1_s = _source0.dtor_s;
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("String too long: "), _1_s);
        }
      }
      {
        return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Invalid Unicode sequence");
      }
    }
  }
  public class SerializationError_OutOfMemory : SerializationError {
    public SerializationError_OutOfMemory() : base() {
    }
    public override _ISerializationError DowncastClone() {
      if (this is _ISerializationError dt) { return dt; }
      return new SerializationError_OutOfMemory();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.SerializationError_OutOfMemory;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.SerializationError.OutOfMemory";
      return s;
    }
  }
  public class SerializationError_IntTooLarge : SerializationError {
    public readonly BigInteger _i;
    public SerializationError_IntTooLarge(BigInteger i) : base() {
      this._i = i;
    }
    public override _ISerializationError DowncastClone() {
      if (this is _ISerializationError dt) { return dt; }
      return new SerializationError_IntTooLarge(_i);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.SerializationError_IntTooLarge;
      return oth != null && this._i == oth._i;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._i));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.SerializationError.IntTooLarge";
      s += "(";
      s += Dafny.Helpers.ToString(this._i);
      s += ")";
      return s;
    }
  }
  public class SerializationError_StringTooLong : SerializationError {
    public readonly Dafny.ISequence<Dafny.Rune> _s;
    public SerializationError_StringTooLong(Dafny.ISequence<Dafny.Rune> s) : base() {
      this._s = s;
    }
    public override _ISerializationError DowncastClone() {
      if (this is _ISerializationError dt) { return dt; }
      return new SerializationError_StringTooLong(_s);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.SerializationError_StringTooLong;
      return oth != null && object.Equals(this._s, oth._s);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._s));
      return (int) hash;
    }
    public override string ToString() {
      string ss = "Errors.SerializationError.StringTooLong";
      ss += "(";
      ss += this._s.ToVerbatimString(true);
      ss += ")";
      return ss;
    }
  }
  public class SerializationError_InvalidUnicode : SerializationError {
    public SerializationError_InvalidUnicode() : base() {
    }
    public override _ISerializationError DowncastClone() {
      if (this is _ISerializationError dt) { return dt; }
      return new SerializationError_InvalidUnicode();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Errors.SerializationError_InvalidUnicode;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 3;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Errors.SerializationError.InvalidUnicode";
      return s;
    }
  }
} // end of namespace Std.JSON.Errors
namespace Std.JSON.Spec {

  public partial class __default {
    public static Dafny.ISequence<ushort> EscapeUnicode(ushort c) {
      Dafny.ISequence<Dafny.Rune> _0_sStr = Std.Strings.HexConversion.__default.OfNat(new BigInteger(c));
      Dafny.ISequence<ushort> _1_s = Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(_0_sStr);
      return Dafny.Sequence<ushort>.Concat(_1_s, ((System.Func<Dafny.ISequence<ushort>>) (() => {
        BigInteger dim5 = (new BigInteger(4)) - (new BigInteger((_1_s).Count));
        var arr5 = new ushort[Dafny.Helpers.ToIntChecked(dim5, "array size exceeds memory limit")];
        for (int i5 = 0; i5 < dim5; i5++) {
          var _2___v8 = (BigInteger) i5;
          arr5[(int)(_2___v8)] = (ushort)((new Dafny.Rune(' ')).Value);
        }
        return Dafny.Sequence<ushort>.FromArray(arr5);
      }))());
    }
    public static Dafny.ISequence<ushort> Escape(Dafny.ISequence<ushort> str, BigInteger start)
    {
      Dafny.ISequence<ushort> _0___accumulator = Dafny.Sequence<ushort>.FromElements();
    TAIL_CALL_START: ;
      if ((start) >= (new BigInteger((str).Count))) {
        return Dafny.Sequence<ushort>.Concat(_0___accumulator, Dafny.Sequence<ushort>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<ushort>.Concat(_0___accumulator, ((System.Func<Dafny.ISequence<ushort>>)(() => {
          ushort _source0 = (str).Select(start);
          {
            if ((_source0) == ((ushort)(34))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\\""));
            }
          }
          {
            if ((_source0) == ((ushort)(92))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\\\"));
            }
          }
          {
            if ((_source0) == ((ushort)(8))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\b"));
            }
          }
          {
            if ((_source0) == ((ushort)(12))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\f"));
            }
          }
          {
            if ((_source0) == ((ushort)(10))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\n"));
            }
          }
          {
            if ((_source0) == ((ushort)(13))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\r"));
            }
          }
          {
            if ((_source0) == ((ushort)(9))) {
              return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\t"));
            }
          }
          {
            ushort _1_c = _source0;
            if ((_1_c) < ((ushort)(31))) {
              return Dafny.Sequence<ushort>.Concat(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF16(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\\u")), Std.JSON.Spec.__default.EscapeUnicode(_1_c));
            } else {
              return Dafny.Sequence<ushort>.FromElements((str).Select(start));
            }
          }
        }))());
        Dafny.ISequence<ushort> _in0 = str;
        BigInteger _in1 = (start) + (BigInteger.One);
        str = _in0;
        start = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> EscapeToUTF8(Dafny.ISequence<Dafny.Rune> str, BigInteger start)
    {
      Std.Wrappers._IResult<Dafny.ISequence<ushort>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ToUTF16Checked(str)).ToResult<Std.JSON.Errors._ISerializationError>(Std.JSON.Errors.SerializationError.create_InvalidUnicode());
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Dafny.ISequence<ushort> _1_utf16 = (_0_valueOrError0).Extract();
        Dafny.ISequence<ushort> _2_escaped = Std.JSON.Spec.__default.Escape(_1_utf16, BigInteger.Zero);
        Std.Wrappers._IResult<Dafny.ISequence<Dafny.Rune>, Std.JSON.Errors._ISerializationError> _3_valueOrError1 = (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.FromUTF16Checked(_2_escaped)).ToResult<Std.JSON.Errors._ISerializationError>(Std.JSON.Errors.SerializationError.create_InvalidUnicode());
        if ((_3_valueOrError1).IsFailure()) {
          return (_3_valueOrError1).PropagateFailure<Dafny.ISequence<byte>>();
        } else {
          Dafny.ISequence<Dafny.Rune> _4_utf32 = (_3_valueOrError1).Extract();
          return (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ToUTF8Checked(_4_utf32)).ToResult<Std.JSON.Errors._ISerializationError>(Std.JSON.Errors.SerializationError.create_InvalidUnicode());
        }
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> String(Dafny.ISequence<Dafny.Rune> str) {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Spec.__default.EscapeToUTF8(str, BigInteger.Zero);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Dafny.ISequence<byte> _1_inBytes = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\"")), _1_inBytes), Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("\""))));
      }
    }
    public static Dafny.ISequence<byte> IntToBytes(BigInteger n) {
      Dafny.ISequence<Dafny.Rune> _0_s = Std.Strings.__default.OfInt(n);
      return Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(_0_s);
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Number(Std.JSON.Values._IDecimal dec) {
      return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Std.JSON.Spec.__default.IntToBytes((dec).dtor_n), ((((dec).dtor_e10).Sign == 0) ? (Dafny.Sequence<byte>.FromElements()) : (Dafny.Sequence<byte>.Concat(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("e")), Std.JSON.Spec.__default.IntToBytes((dec).dtor_e10))))));
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> KeyValue(_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON> kv) {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Spec.__default.String((kv).dtor__0);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Dafny.ISequence<byte> _1_key = (_0_valueOrError0).Extract();
        Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _2_valueOrError1 = Std.JSON.Spec.__default.JSON((kv).dtor__1);
        if ((_2_valueOrError1).IsFailure()) {
          return (_2_valueOrError1).PropagateFailure<Dafny.ISequence<byte>>();
        } else {
          Dafny.ISequence<byte> _3_value = (_2_valueOrError1).Extract();
          return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(_1_key, Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString(":"))), _3_value));
        }
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Join(Dafny.ISequence<byte> sep, Dafny.ISequence<Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>> items)
    {
      if ((new BigInteger((items).Count)).Sign == 0) {
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.FromElements());
      } else {
        Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = (items).Select(BigInteger.Zero);
        if ((_0_valueOrError0).IsFailure()) {
          return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
        } else {
          Dafny.ISequence<byte> _1_first = (_0_valueOrError0).Extract();
          if ((new BigInteger((items).Count)) == (BigInteger.One)) {
            return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(_1_first);
          } else {
            Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _2_valueOrError1 = Std.JSON.Spec.__default.Join(sep, (items).Drop(BigInteger.One));
            if ((_2_valueOrError1).IsFailure()) {
              return (_2_valueOrError1).PropagateFailure<Dafny.ISequence<byte>>();
            } else {
              Dafny.ISequence<byte> _3_rest = (_2_valueOrError1).Extract();
              return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(_1_first, sep), _3_rest));
            }
          }
        }
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Object(Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> obj) {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Spec.__default.Join(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString(",")), ((System.Func<Dafny.ISequence<Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>>>) (() => {
        BigInteger dim6 = new BigInteger((obj).Count);
        var arr6 = new Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>[Dafny.Helpers.ToIntChecked(dim6, "array size exceeds memory limit")];
        for (int i6 = 0; i6 < dim6; i6++) {
          var _1_i = (BigInteger) i6;
          arr6[(int)(_1_i)] = Std.JSON.Spec.__default.KeyValue((obj).Select(_1_i));
        }
        return Dafny.Sequence<Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>>.FromArray(arr6);
      }))());
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Dafny.ISequence<byte> _2_middle = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("{")), _2_middle), Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("}"))));
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Array(Dafny.ISequence<Std.JSON.Values._IJSON> arr) {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Spec.__default.Join(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString(",")), ((System.Func<Dafny.ISequence<Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>>>) (() => {
        BigInteger dim7 = new BigInteger((arr).Count);
        var arr7 = new Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>[Dafny.Helpers.ToIntChecked(dim7, "array size exceeds memory limit")];
        for (int i7 = 0; i7 < dim7; i7++) {
          var _1_i = (BigInteger) i7;
          arr7[(int)(_1_i)] = Std.JSON.Spec.__default.JSON((arr).Select(_1_i));
        }
        return Dafny.Sequence<Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>>.FromArray(arr7);
      }))());
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Dafny.ISequence<byte> _2_middle = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("[")), _2_middle), Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("]"))));
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> JSON(Std.JSON.Values._IJSON js) {
      Std.JSON.Values._IJSON _source0 = js;
      {
        if (_source0.is_Null) {
          return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("null")));
        }
      }
      {
        if (_source0.is_Bool) {
          bool _0_b = _source0.dtor_b;
          return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success(((_0_b) ? (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("true"))) : (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ASCIIToUTF8(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("false")))));
        }
      }
      {
        if (_source0.is_String) {
          Dafny.ISequence<Dafny.Rune> _1_str = _source0.dtor_str;
          return Std.JSON.Spec.__default.String(_1_str);
        }
      }
      {
        if (_source0.is_Number) {
          Std.JSON.Values._IDecimal _2_dec = _source0.dtor_num;
          return Std.JSON.Spec.__default.Number(_2_dec);
        }
      }
      {
        if (_source0.is_Object) {
          Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> _3_obj = _source0.dtor_obj;
          return Std.JSON.Spec.__default.Object(_3_obj);
        }
      }
      {
        Dafny.ISequence<Std.JSON.Values._IJSON> _4_arr = _source0.dtor_arr;
        return Std.JSON.Spec.__default.Array(_4_arr);
      }
    }
  }
} // end of namespace Std.JSON.Spec
namespace Std.JSON.Utils.Views.Core {

  public partial class __default {
    public static bool Adjacent(Std.JSON.Utils.Views.Core._IView__ lv, Std.JSON.Utils.Views.Core._IView__ rv)
    {
      return (((lv).dtor_end) == ((rv).dtor_beg)) && (((lv).dtor_s).Equals((rv).dtor_s));
    }
    public static Std.JSON.Utils.Views.Core._IView__ Merge(Std.JSON.Utils.Views.Core._IView__ lv, Std.JSON.Utils.Views.Core._IView__ rv)
    {
      Std.JSON.Utils.Views.Core._IView__ _0_dt__update__tmp_h0 = lv;
      uint _1_dt__update_hend_h0 = (rv).dtor_end;
      return Std.JSON.Utils.Views.Core.View__.create((_0_dt__update__tmp_h0).dtor_s, (_0_dt__update__tmp_h0).dtor_beg, _1_dt__update_hend_h0);
    }
  }

  public partial class View {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.create(Dafny.Sequence<byte>.FromElements(), 0U, 0U);
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Utils.Views.Core.View.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public interface _IView__ {
    bool is_View { get; }
    Dafny.ISequence<byte> dtor_s { get; }
    uint dtor_beg { get; }
    uint dtor_end { get; }
    _IView__ DowncastClone();
    bool Empty_q { get; }
    uint Length();
    Dafny.ISequence<byte> Bytes();
    bool Byte_q(byte c);
    bool Char_q(Dafny.Rune c);
    byte At(uint idx);
    short Peek();
    void CopyTo(byte[] dest, uint start);
  }
  public class View__ : _IView__ {
    public readonly Dafny.ISequence<byte> _s;
    public readonly uint _beg;
    public readonly uint _end;
    public View__(Dafny.ISequence<byte> s, uint beg, uint end) {
      this._s = s;
      this._beg = beg;
      this._end = end;
    }
    public _IView__ DowncastClone() {
      if (this is _IView__ dt) { return dt; }
      return new View__(_s, _beg, _end);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Views.Core.View__;
      return oth != null && object.Equals(this._s, oth._s) && this._beg == oth._beg && this._end == oth._end;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._s));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._beg));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._end));
      return (int) hash;
    }
    public override string ToString() {
      string ss = "Core.View_.View";
      ss += "(";
      ss += Dafny.Helpers.ToString(this._s);
      ss += ", ";
      ss += Dafny.Helpers.ToString(this._beg);
      ss += ", ";
      ss += Dafny.Helpers.ToString(this._end);
      ss += ")";
      return ss;
    }
    private static readonly Std.JSON.Utils.Views.Core._IView__ theDefault = create(Dafny.Sequence<byte>.Empty, 0, 0);
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Utils.Views.Core.View__.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IView__ create(Dafny.ISequence<byte> s, uint beg, uint end) {
      return new View__(s, beg, end);
    }
    public static _IView__ create_View(Dafny.ISequence<byte> s, uint beg, uint end) {
      return create(s, beg, end);
    }
    public bool is_View { get { return true; } }
    public Dafny.ISequence<byte> dtor_s {
      get {
        return this._s;
      }
    }
    public uint dtor_beg {
      get {
        return this._beg;
      }
    }
    public uint dtor_end {
      get {
        return this._end;
      }
    }
    public uint Length() {
      return ((this).dtor_end) - ((this).dtor_beg);
    }
    public Dafny.ISequence<byte> Bytes() {
      return ((this).dtor_s).Subsequence((this).dtor_beg, (this).dtor_end);
    }
    public static Std.JSON.Utils.Views.Core._IView__ OfBytes(Dafny.ISequence<byte> bs) {
      return Std.JSON.Utils.Views.Core.View__.create(bs, (uint)(0U), (uint)(bs).LongCount);
    }
    public static Dafny.ISequence<byte> OfString(Dafny.ISequence<Dafny.Rune> s) {
      return ((System.Func<Dafny.ISequence<byte>>) (() => {
        BigInteger dim8 = new BigInteger((s).Count);
        var arr8 = new byte[Dafny.Helpers.ToIntChecked(dim8, "array size exceeds memory limit")];
        for (int i8 = 0; i8 < dim8; i8++) {
          var _0_i = (BigInteger) i8;
          arr8[(int)(_0_i)] = (byte)(((s).Select(_0_i)).Value);
        }
        return Dafny.Sequence<byte>.FromArray(arr8);
      }))();
    }
    public bool Byte_q(byte c)
    {
      bool _hresult = false;
      _hresult = (((this).Length()) == (1U)) && (((this).At(0U)) == (c));
      return _hresult;
      return _hresult;
    }
    public bool Char_q(Dafny.Rune c) {
      return (this).Byte_q((byte)((c).Value));
    }
    public byte At(uint idx) {
      return ((this).dtor_s).Select(((this).dtor_beg) + (idx));
    }
    public short Peek() {
      if ((this).Empty_q) {
        return (short)(-1);
      } else {
        return (short)((this).At(0U));
      }
    }
    public void CopyTo(byte[] dest, uint start)
    {
      uint _hi0 = (this).Length();
      for (uint _0_idx = 0U; _0_idx < _hi0; _0_idx++) {
        uint _index0 = (start) + (_0_idx);
        (dest)[(int)(_index0)] = ((this).dtor_s).Select(((this).dtor_beg) + (_0_idx));
      }
    }
    public static Std.JSON.Utils.Views.Core._IView__ Empty { get {
      return Std.JSON.Utils.Views.Core.View__.create(Dafny.Sequence<byte>.FromElements(), 0U, 0U);
    } }
    public bool Empty_q { get {
      return ((this).dtor_beg) == ((this).dtor_end);
    } }
  }
} // end of namespace Std.JSON.Utils.Views.Core
namespace Std.JSON.Utils.Views.Writers {


  public interface _IChain {
    bool is_Empty { get; }
    bool is_Chain { get; }
    Std.JSON.Utils.Views.Writers._IChain dtor_previous { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_v { get; }
    _IChain DowncastClone();
    BigInteger Length();
    BigInteger Count();
    Dafny.ISequence<byte> Bytes();
    Std.JSON.Utils.Views.Writers._IChain Append(Std.JSON.Utils.Views.Core._IView__ v_k);
    void CopyTo(byte[] dest, uint end);
  }
  public abstract class Chain : _IChain {
    public Chain() {
    }
    private static readonly Std.JSON.Utils.Views.Writers._IChain theDefault = create_Empty();
    public static Std.JSON.Utils.Views.Writers._IChain Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IChain> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IChain>(Std.JSON.Utils.Views.Writers.Chain.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IChain> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IChain create_Empty() {
      return new Chain_Empty();
    }
    public static _IChain create_Chain(Std.JSON.Utils.Views.Writers._IChain previous, Std.JSON.Utils.Views.Core._IView__ v) {
      return new Chain_Chain(previous, v);
    }
    public bool is_Empty { get { return this is Chain_Empty; } }
    public bool is_Chain { get { return this is Chain_Chain; } }
    public Std.JSON.Utils.Views.Writers._IChain dtor_previous {
      get {
        var d = this;
        return ((Chain_Chain)d)._previous;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_v {
      get {
        var d = this;
        return ((Chain_Chain)d)._v;
      }
    }
    public abstract _IChain DowncastClone();
    public BigInteger Length() {
      BigInteger _0___accumulator = BigInteger.Zero;
      _IChain _this = this;
    TAIL_CALL_START: ;
      if ((_this).is_Empty) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (new BigInteger(((_this).dtor_v).Length())) + (_0___accumulator);
        Std.JSON.Utils.Views.Writers._IChain _in0 = (_this).dtor_previous;
        _this = _in0;
        ;
        goto TAIL_CALL_START;
      }
    }
    public BigInteger Count() {
      BigInteger _0___accumulator = BigInteger.Zero;
      _IChain _this = this;
    TAIL_CALL_START: ;
      if ((_this).is_Empty) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = (BigInteger.One) + (_0___accumulator);
        Std.JSON.Utils.Views.Writers._IChain _in0 = (_this).dtor_previous;
        _this = _in0;
        ;
        goto TAIL_CALL_START;
      }
    }
    public Dafny.ISequence<byte> Bytes() {
      Dafny.ISequence<byte> _0___accumulator = Dafny.Sequence<byte>.FromElements();
      _IChain _this = this;
    TAIL_CALL_START: ;
      if ((_this).is_Empty) {
        return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<byte>.Concat(((_this).dtor_v).Bytes(), _0___accumulator);
        Std.JSON.Utils.Views.Writers._IChain _in0 = (_this).dtor_previous;
        _this = _in0;
        ;
        goto TAIL_CALL_START;
      }
    }
    public Std.JSON.Utils.Views.Writers._IChain Append(Std.JSON.Utils.Views.Core._IView__ v_k) {
      if (((this).is_Chain) && (Std.JSON.Utils.Views.Core.__default.Adjacent((this).dtor_v, v_k))) {
        return Std.JSON.Utils.Views.Writers.Chain.create_Chain((this).dtor_previous, Std.JSON.Utils.Views.Core.__default.Merge((this).dtor_v, v_k));
      } else {
        return Std.JSON.Utils.Views.Writers.Chain.create_Chain(this, v_k);
      }
    }
    public void CopyTo(byte[] dest, uint end)
    {
      _IChain _this = this;
    TAIL_CALL_START: ;
      if ((_this).is_Chain) {
        uint _0_end;
        _0_end = (end) - (((_this).dtor_v).Length());
        ((_this).dtor_v).CopyTo(dest, _0_end);
        Std.JSON.Utils.Views.Writers._IChain _in0 = (_this).dtor_previous;
        byte[] _in1 = dest;
        uint _in2 = _0_end;
        _this = _in0;
        ;
        dest = _in1;
        end = _in2;
        goto TAIL_CALL_START;
      }
    }
  }
  public class Chain_Empty : Chain {
    public Chain_Empty() : base() {
    }
    public override _IChain DowncastClone() {
      if (this is _IChain dt) { return dt; }
      return new Chain_Empty();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Views.Writers.Chain_Empty;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Writers.Chain.Empty";
      return s;
    }
  }
  public class Chain_Chain : Chain {
    public readonly Std.JSON.Utils.Views.Writers._IChain _previous;
    public readonly Std.JSON.Utils.Views.Core._IView__ _v;
    public Chain_Chain(Std.JSON.Utils.Views.Writers._IChain previous, Std.JSON.Utils.Views.Core._IView__ v) : base() {
      this._previous = previous;
      this._v = v;
    }
    public override _IChain DowncastClone() {
      if (this is _IChain dt) { return dt; }
      return new Chain_Chain(_previous, _v);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Views.Writers.Chain_Chain;
      return oth != null && object.Equals(this._previous, oth._previous) && object.Equals(this._v, oth._v);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._previous));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._v));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Writers.Chain.Chain";
      s += "(";
      s += Dafny.Helpers.ToString(this._previous);
      s += ", ";
      s += Dafny.Helpers.ToString(this._v);
      s += ")";
      return s;
    }
  }

  public partial class Writer {
    private static readonly Std.JSON.Utils.Views.Writers._IWriter__ Witness = Std.JSON.Utils.Views.Writers.Writer__.create(0U, Std.JSON.Utils.Views.Writers.Chain.create_Empty());
    public static Std.JSON.Utils.Views.Writers._IWriter__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__>(Std.JSON.Utils.Views.Writers.Writer.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public interface _IWriter__ {
    bool is_Writer { get; }
    uint dtor_length { get; }
    Std.JSON.Utils.Views.Writers._IChain dtor_chain { get; }
    _IWriter__ DowncastClone();
    bool Empty_q { get; }
    bool Unsaturated_q { get; }
    Dafny.ISequence<byte> Bytes();
    Std.JSON.Utils.Views.Writers._IWriter__ Append(Std.JSON.Utils.Views.Core._IView__ v_k);
    Std.JSON.Utils.Views.Writers._IWriter__ Then(Func<Std.JSON.Utils.Views.Writers._IWriter__, Std.JSON.Utils.Views.Writers._IWriter__> fn);
    void CopyTo(byte[] dest);
    byte[] ToArray();
  }
  public class Writer__ : _IWriter__ {
    public readonly uint _length;
    public readonly Std.JSON.Utils.Views.Writers._IChain _chain;
    public Writer__(uint length, Std.JSON.Utils.Views.Writers._IChain chain) {
      this._length = length;
      this._chain = chain;
    }
    public _IWriter__ DowncastClone() {
      if (this is _IWriter__ dt) { return dt; }
      return new Writer__(_length, _chain);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Views.Writers.Writer__;
      return oth != null && this._length == oth._length && object.Equals(this._chain, oth._chain);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._length));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._chain));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Writers.Writer_.Writer";
      s += "(";
      s += Dafny.Helpers.ToString(this._length);
      s += ", ";
      s += Dafny.Helpers.ToString(this._chain);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Utils.Views.Writers._IWriter__ theDefault = create(0, Std.JSON.Utils.Views.Writers.Chain.Default());
    public static Std.JSON.Utils.Views.Writers._IWriter__ Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__>(Std.JSON.Utils.Views.Writers.Writer__.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Writers._IWriter__> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IWriter__ create(uint length, Std.JSON.Utils.Views.Writers._IChain chain) {
      return new Writer__(length, chain);
    }
    public static _IWriter__ create_Writer(uint length, Std.JSON.Utils.Views.Writers._IChain chain) {
      return create(length, chain);
    }
    public bool is_Writer { get { return true; } }
    public uint dtor_length {
      get {
        return this._length;
      }
    }
    public Std.JSON.Utils.Views.Writers._IChain dtor_chain {
      get {
        return this._chain;
      }
    }
    public Dafny.ISequence<byte> Bytes() {
      return ((this).dtor_chain).Bytes();
    }
    public static uint SaturatedAddU32(uint a, uint b)
    {
      if ((a) <= ((Std.BoundedInts.__default.UINT32__MAX) - (b))) {
        return (a) + (b);
      } else {
        return Std.BoundedInts.__default.UINT32__MAX;
      }
    }
    public Std.JSON.Utils.Views.Writers._IWriter__ Append(Std.JSON.Utils.Views.Core._IView__ v_k) {
      return Std.JSON.Utils.Views.Writers.Writer__.create(Std.JSON.Utils.Views.Writers.Writer__.SaturatedAddU32((this).dtor_length, (v_k).Length()), ((this).dtor_chain).Append(v_k));
    }
    public Std.JSON.Utils.Views.Writers._IWriter__ Then(Func<Std.JSON.Utils.Views.Writers._IWriter__, Std.JSON.Utils.Views.Writers._IWriter__> fn) {
      return Dafny.Helpers.Id<Func<Std.JSON.Utils.Views.Writers._IWriter__, Std.JSON.Utils.Views.Writers._IWriter__>>(fn)(this);
    }
    public void CopyTo(byte[] dest)
    {
      ((this).dtor_chain).CopyTo(dest, (this).dtor_length);
    }
    public byte[] ToArray()
    {
      byte[] bs = new byte[0];
      Func<BigInteger, byte> _init0 = ((System.Func<BigInteger, byte>)((_0_i) => {
        return (byte)(0);
      }));
      byte[] _nw0 = new byte[Dafny.Helpers.ToIntChecked((this).dtor_length, "array size exceeds memory limit")];
      for (var _i0_0 = 0; _i0_0 < new BigInteger(_nw0.Length); _i0_0++) {
        _nw0[(int)(_i0_0)] = _init0(_i0_0);
      }
      bs = _nw0;
      (this).CopyTo(bs);
      return bs;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Empty { get {
      return Std.JSON.Utils.Views.Writers.Writer__.create(0U, Std.JSON.Utils.Views.Writers.Chain.create_Empty());
    } }
    public bool Unsaturated_q { get {
      return ((this).dtor_length) != (Std.BoundedInts.__default.UINT32__MAX);
    } }
    public bool Empty_q { get {
      return ((this).dtor_chain).is_Empty;
    } }
  }
} // end of namespace Std.JSON.Utils.Views.Writers
namespace Std.JSON.Utils.Lexers.Core {


  public interface _ILexerResult<out T, out R> {
    bool is_Accept { get; }
    bool is_Reject { get; }
    bool is_Partial { get; }
    R dtor_err { get; }
    T dtor_st { get; }
    _ILexerResult<__T, __R> DowncastClone<__T, __R>(Func<T, __T> converter0, Func<R, __R> converter1);
  }
  public abstract class LexerResult<T, R> : _ILexerResult<T, R> {
    public LexerResult() {
    }
    public static Std.JSON.Utils.Lexers.Core._ILexerResult<T, R> Default() {
      return create_Accept();
    }
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Lexers.Core._ILexerResult<T, R>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.JSON.Utils.Lexers.Core._ILexerResult<T, R>>(Std.JSON.Utils.Lexers.Core.LexerResult<T, R>.Default());
    }
    public static _ILexerResult<T, R> create_Accept() {
      return new LexerResult_Accept<T, R>();
    }
    public static _ILexerResult<T, R> create_Reject(R err) {
      return new LexerResult_Reject<T, R>(err);
    }
    public static _ILexerResult<T, R> create_Partial(T st) {
      return new LexerResult_Partial<T, R>(st);
    }
    public bool is_Accept { get { return this is LexerResult_Accept<T, R>; } }
    public bool is_Reject { get { return this is LexerResult_Reject<T, R>; } }
    public bool is_Partial { get { return this is LexerResult_Partial<T, R>; } }
    public R dtor_err {
      get {
        var d = this;
        return ((LexerResult_Reject<T, R>)d)._err;
      }
    }
    public T dtor_st {
      get {
        var d = this;
        return ((LexerResult_Partial<T, R>)d)._st;
      }
    }
    public abstract _ILexerResult<__T, __R> DowncastClone<__T, __R>(Func<T, __T> converter0, Func<R, __R> converter1);
  }
  public class LexerResult_Accept<T, R> : LexerResult<T, R> {
    public LexerResult_Accept() : base() {
    }
    public override _ILexerResult<__T, __R> DowncastClone<__T, __R>(Func<T, __T> converter0, Func<R, __R> converter1) {
      if (this is _ILexerResult<__T, __R> dt) { return dt; }
      return new LexerResult_Accept<__T, __R>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Core.LexerResult_Accept<T, R>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Core.LexerResult.Accept";
      return s;
    }
  }
  public class LexerResult_Reject<T, R> : LexerResult<T, R> {
    public readonly R _err;
    public LexerResult_Reject(R err) : base() {
      this._err = err;
    }
    public override _ILexerResult<__T, __R> DowncastClone<__T, __R>(Func<T, __T> converter0, Func<R, __R> converter1) {
      if (this is _ILexerResult<__T, __R> dt) { return dt; }
      return new LexerResult_Reject<__T, __R>(converter1(_err));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Core.LexerResult_Reject<T, R>;
      return oth != null && object.Equals(this._err, oth._err);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._err));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Core.LexerResult.Reject";
      s += "(";
      s += Dafny.Helpers.ToString(this._err);
      s += ")";
      return s;
    }
  }
  public class LexerResult_Partial<T, R> : LexerResult<T, R> {
    public readonly T _st;
    public LexerResult_Partial(T st) : base() {
      this._st = st;
    }
    public override _ILexerResult<__T, __R> DowncastClone<__T, __R>(Func<T, __T> converter0, Func<R, __R> converter1) {
      if (this is _ILexerResult<__T, __R> dt) { return dt; }
      return new LexerResult_Partial<__T, __R>(converter0(_st));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Core.LexerResult_Partial<T, R>;
      return oth != null && object.Equals(this._st, oth._st);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._st));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Core.LexerResult.Partial";
      s += "(";
      s += Dafny.Helpers.ToString(this._st);
      s += ")";
      return s;
    }
  }
} // end of namespace Std.JSON.Utils.Lexers.Core
namespace Std.JSON.Utils.Lexers.Strings {

  public partial class __default {
    public static Std.JSON.Utils.Lexers.Core._ILexerResult<bool, __R> StringBody<__R>(bool escaped, short @byte)
    {
      if ((@byte) == ((short)((new Dafny.Rune('\\')).Value))) {
        return Std.JSON.Utils.Lexers.Core.LexerResult<bool, __R>.create_Partial(!(escaped));
      } else if (((@byte) == ((short)((new Dafny.Rune('\"')).Value))) && (!(escaped))) {
        return Std.JSON.Utils.Lexers.Core.LexerResult<bool, __R>.create_Accept();
      } else {
        return Std.JSON.Utils.Lexers.Core.LexerResult<bool, __R>.create_Partial(false);
      }
    }
    public static Std.JSON.Utils.Lexers.Core._ILexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>> String(Std.JSON.Utils.Lexers.Strings._IStringLexerState st, short @byte)
    {
      Std.JSON.Utils.Lexers.Strings._IStringLexerState _source0 = st;
      {
        if (_source0.is_Start) {
          if ((@byte) == ((short)((new Dafny.Rune('\"')).Value))) {
            return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Partial(Std.JSON.Utils.Lexers.Strings.StringLexerState.create_Body(false));
          } else {
            return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Reject(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("String must start with double quote"));
          }
        }
      }
      {
        if (_source0.is_End) {
          return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Accept();
        }
      }
      {
        bool _0_escaped = _source0.dtor_escaped;
        if ((@byte) == ((short)((new Dafny.Rune('\\')).Value))) {
          return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Partial(Std.JSON.Utils.Lexers.Strings.StringLexerState.create_Body(!(_0_escaped)));
        } else if (((@byte) == ((short)((new Dafny.Rune('\"')).Value))) && (!(_0_escaped))) {
          return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Partial(Std.JSON.Utils.Lexers.Strings.StringLexerState.create_End());
        } else {
          return Std.JSON.Utils.Lexers.Core.LexerResult<Std.JSON.Utils.Lexers.Strings._IStringLexerState, Dafny.ISequence<Dafny.Rune>>.create_Partial(Std.JSON.Utils.Lexers.Strings.StringLexerState.create_Body(false));
        }
      }
    }
    public static bool StringBodyLexerStart { get {
      return false;
    } }
    public static Std.JSON.Utils.Lexers.Strings._IStringLexerState StringLexerStart { get {
      return Std.JSON.Utils.Lexers.Strings.StringLexerState.create_Start();
    } }
  }

  public interface _IStringLexerState {
    bool is_Start { get; }
    bool is_Body { get; }
    bool is_End { get; }
    bool dtor_escaped { get; }
    _IStringLexerState DowncastClone();
  }
  public abstract class StringLexerState : _IStringLexerState {
    public StringLexerState() {
    }
    private static readonly Std.JSON.Utils.Lexers.Strings._IStringLexerState theDefault = create_Start();
    public static Std.JSON.Utils.Lexers.Strings._IStringLexerState Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Lexers.Strings._IStringLexerState> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Lexers.Strings._IStringLexerState>(Std.JSON.Utils.Lexers.Strings.StringLexerState.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Lexers.Strings._IStringLexerState> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IStringLexerState create_Start() {
      return new StringLexerState_Start();
    }
    public static _IStringLexerState create_Body(bool escaped) {
      return new StringLexerState_Body(escaped);
    }
    public static _IStringLexerState create_End() {
      return new StringLexerState_End();
    }
    public bool is_Start { get { return this is StringLexerState_Start; } }
    public bool is_Body { get { return this is StringLexerState_Body; } }
    public bool is_End { get { return this is StringLexerState_End; } }
    public bool dtor_escaped {
      get {
        var d = this;
        return ((StringLexerState_Body)d)._escaped;
      }
    }
    public abstract _IStringLexerState DowncastClone();
  }
  public class StringLexerState_Start : StringLexerState {
    public StringLexerState_Start() : base() {
    }
    public override _IStringLexerState DowncastClone() {
      if (this is _IStringLexerState dt) { return dt; }
      return new StringLexerState_Start();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Strings.StringLexerState_Start;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Strings.StringLexerState.Start";
      return s;
    }
  }
  public class StringLexerState_Body : StringLexerState {
    public readonly bool _escaped;
    public StringLexerState_Body(bool escaped) : base() {
      this._escaped = escaped;
    }
    public override _IStringLexerState DowncastClone() {
      if (this is _IStringLexerState dt) { return dt; }
      return new StringLexerState_Body(_escaped);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Strings.StringLexerState_Body;
      return oth != null && this._escaped == oth._escaped;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._escaped));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Strings.StringLexerState.Body";
      s += "(";
      s += Dafny.Helpers.ToString(this._escaped);
      s += ")";
      return s;
    }
  }
  public class StringLexerState_End : StringLexerState {
    public StringLexerState_End() : base() {
    }
    public override _IStringLexerState DowncastClone() {
      if (this is _IStringLexerState dt) { return dt; }
      return new StringLexerState_End();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Lexers.Strings.StringLexerState_End;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Strings.StringLexerState.End";
      return s;
    }
  }
} // end of namespace Std.JSON.Utils.Lexers.Strings
namespace Std.JSON.Utils.Lexers {

} // end of namespace Std.JSON.Utils.Lexers
namespace Std.JSON.Utils.Cursors {


  public interface _ISplit<out T> {
    bool is_SP { get; }
    T dtor_t { get; }
    Std.JSON.Utils.Cursors._ICursor__ dtor_cs { get; }
    _ISplit<__T> DowncastClone<__T>(Func<T, __T> converter0);
  }
  public class Split<T> : _ISplit<T> {
    public readonly T _t;
    public readonly Std.JSON.Utils.Cursors._ICursor__ _cs;
    public Split(T t, Std.JSON.Utils.Cursors._ICursor__ cs) {
      this._t = t;
      this._cs = cs;
    }
    public _ISplit<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _ISplit<__T> dt) { return dt; }
      return new Split<__T>(converter0(_t), _cs);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.Split<T>;
      return oth != null && object.Equals(this._t, oth._t) && object.Equals(this._cs, oth._cs);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._t));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._cs));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Cursors.Split.SP";
      s += "(";
      s += Dafny.Helpers.ToString(this._t);
      s += ", ";
      s += Dafny.Helpers.ToString(this._cs);
      s += ")";
      return s;
    }
    public static Std.JSON.Utils.Cursors._ISplit<T> Default(T _default_T) {
      return create(_default_T, Std.JSON.Utils.Cursors.FreshCursor.Default());
    }
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ISplit<T>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T) {
      return new Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ISplit<T>>(Std.JSON.Utils.Cursors.Split<T>.Default(_td_T.Default()));
    }
    public static _ISplit<T> create(T t, Std.JSON.Utils.Cursors._ICursor__ cs) {
      return new Split<T>(t, cs);
    }
    public static _ISplit<T> create_SP(T t, Std.JSON.Utils.Cursors._ICursor__ cs) {
      return create(t, cs);
    }
    public bool is_SP { get { return true; } }
    public T dtor_t {
      get {
        return this._t;
      }
    }
    public Std.JSON.Utils.Cursors._ICursor__ dtor_cs {
      get {
        return this._cs;
      }
    }
  }

  public partial class Cursor {
    private static readonly Std.JSON.Utils.Cursors._ICursor__ Witness = Std.JSON.Utils.Cursors.Cursor__.create(Dafny.Sequence<byte>.FromElements(), 0U, 0U, 0U);
    public static Std.JSON.Utils.Cursors._ICursor__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__>(Std.JSON.Utils.Cursors.Cursor.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class FreshCursor {
    private static readonly Std.JSON.Utils.Cursors._ICursor__ Witness = Std.JSON.Utils.Cursors.Cursor__.create(Dafny.Sequence<byte>.FromElements(), 0U, 0U, 0U);
    public static Std.JSON.Utils.Cursors._ICursor__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__>(Std.JSON.Utils.Cursors.FreshCursor.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public interface _ICursorError<out R> {
    bool is_EOF { get; }
    bool is_ExpectingByte { get; }
    bool is_ExpectingAnyByte { get; }
    bool is_OtherError { get; }
    byte dtor_expected { get; }
    short dtor_b { get; }
    Dafny.ISequence<byte> dtor_expected__sq { get; }
    R dtor_err { get; }
    _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0);
  }
  public abstract class CursorError<R> : _ICursorError<R> {
    public CursorError() {
    }
    public static Std.JSON.Utils.Cursors._ICursorError<R> Default() {
      return create_EOF();
    }
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursorError<R>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursorError<R>>(Std.JSON.Utils.Cursors.CursorError<R>.Default());
    }
    public static _ICursorError<R> create_EOF() {
      return new CursorError_EOF<R>();
    }
    public static _ICursorError<R> create_ExpectingByte(byte expected, short b) {
      return new CursorError_ExpectingByte<R>(expected, b);
    }
    public static _ICursorError<R> create_ExpectingAnyByte(Dafny.ISequence<byte> expected__sq, short b) {
      return new CursorError_ExpectingAnyByte<R>(expected__sq, b);
    }
    public static _ICursorError<R> create_OtherError(R err) {
      return new CursorError_OtherError<R>(err);
    }
    public bool is_EOF { get { return this is CursorError_EOF<R>; } }
    public bool is_ExpectingByte { get { return this is CursorError_ExpectingByte<R>; } }
    public bool is_ExpectingAnyByte { get { return this is CursorError_ExpectingAnyByte<R>; } }
    public bool is_OtherError { get { return this is CursorError_OtherError<R>; } }
    public byte dtor_expected {
      get {
        var d = this;
        return ((CursorError_ExpectingByte<R>)d)._expected;
      }
    }
    public short dtor_b {
      get {
        var d = this;
        if (d is CursorError_ExpectingByte<R>) { return ((CursorError_ExpectingByte<R>)d)._b; }
        return ((CursorError_ExpectingAnyByte<R>)d)._b;
      }
    }
    public Dafny.ISequence<byte> dtor_expected__sq {
      get {
        var d = this;
        return ((CursorError_ExpectingAnyByte<R>)d)._expected__sq;
      }
    }
    public R dtor_err {
      get {
        var d = this;
        return ((CursorError_OtherError<R>)d)._err;
      }
    }
    public abstract _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0);
    public static Dafny.ISequence<Dafny.Rune> _ToString(Std.JSON.Utils.Cursors._ICursorError<R> _this, Func<R, Dafny.ISequence<Dafny.Rune>> pr) {
      Std.JSON.Utils.Cursors._ICursorError<R> _source0 = _this;
      {
        if (_source0.is_EOF) {
          return Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Reached EOF");
        }
      }
      {
        if (_source0.is_ExpectingByte) {
          byte _0_b0 = _source0.dtor_expected;
          short _1_b = _source0.dtor_b;
          Dafny.ISequence<Dafny.Rune> _2_c = (((_1_b) > ((short)(0))) ? (Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_1_b)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"))) : (Dafny.Sequence<Dafny.Rune>.UnicodeFromString("EOF")));
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Expecting '"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_0_b0)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("', read ")), _2_c);
        }
      }
      {
        if (_source0.is_ExpectingAnyByte) {
          Dafny.ISequence<byte> _3_bs0 = _source0.dtor_expected__sq;
          short _4_b = _source0.dtor_b;
          Dafny.ISequence<Dafny.Rune> _5_c = (((_4_b) > ((short)(0))) ? (Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"), Dafny.Sequence<Dafny.Rune>.FromElements(new Dafny.Rune((int)(_4_b)))), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("'"))) : (Dafny.Sequence<Dafny.Rune>.UnicodeFromString("EOF")));
          Dafny.ISequence<Dafny.Rune> _6_c0s = ((System.Func<Dafny.ISequence<Dafny.Rune>>) (() => {
            BigInteger dim9 = new BigInteger((_3_bs0).Count);
            var arr9 = new Dafny.Rune[Dafny.Helpers.ToIntChecked(dim9, "array size exceeds memory limit")];
            for (int i9 = 0; i9 < dim9; i9++) {
              var _7_idx = (BigInteger) i9;
              arr9[(int)(_7_idx)] = new Dafny.Rune((int)((_3_bs0).Select(_7_idx)));
            }
            return Dafny.Sequence<Dafny.Rune>.FromArray(arr9);
          }))();
          return Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.Concat(Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Expecting one of '"), _6_c0s), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("', read ")), _5_c);
        }
      }
      {
        R _8_err = _source0.dtor_err;
        return Dafny.Helpers.Id<Func<R, Dafny.ISequence<Dafny.Rune>>>(pr)(_8_err);
      }
    }
  }
  public class CursorError_EOF<R> : CursorError<R> {
    public CursorError_EOF() : base() {
    }
    public override _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0) {
      if (this is _ICursorError<__R> dt) { return dt; }
      return new CursorError_EOF<__R>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.CursorError_EOF<R>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Cursors.CursorError.EOF";
      return s;
    }
  }
  public class CursorError_ExpectingByte<R> : CursorError<R> {
    public readonly byte _expected;
    public readonly short _b;
    public CursorError_ExpectingByte(byte expected, short b) : base() {
      this._expected = expected;
      this._b = b;
    }
    public override _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0) {
      if (this is _ICursorError<__R> dt) { return dt; }
      return new CursorError_ExpectingByte<__R>(_expected, _b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.CursorError_ExpectingByte<R>;
      return oth != null && this._expected == oth._expected && this._b == oth._b;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._expected));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Cursors.CursorError.ExpectingByte";
      s += "(";
      s += Dafny.Helpers.ToString(this._expected);
      s += ", ";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class CursorError_ExpectingAnyByte<R> : CursorError<R> {
    public readonly Dafny.ISequence<byte> _expected__sq;
    public readonly short _b;
    public CursorError_ExpectingAnyByte(Dafny.ISequence<byte> expected__sq, short b) : base() {
      this._expected__sq = expected__sq;
      this._b = b;
    }
    public override _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0) {
      if (this is _ICursorError<__R> dt) { return dt; }
      return new CursorError_ExpectingAnyByte<__R>(_expected__sq, _b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.CursorError_ExpectingAnyByte<R>;
      return oth != null && object.Equals(this._expected__sq, oth._expected__sq) && this._b == oth._b;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._expected__sq));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Cursors.CursorError.ExpectingAnyByte";
      s += "(";
      s += Dafny.Helpers.ToString(this._expected__sq);
      s += ", ";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class CursorError_OtherError<R> : CursorError<R> {
    public readonly R _err;
    public CursorError_OtherError(R err) : base() {
      this._err = err;
    }
    public override _ICursorError<__R> DowncastClone<__R>(Func<R, __R> converter0) {
      if (this is _ICursorError<__R> dt) { return dt; }
      return new CursorError_OtherError<__R>(converter0(_err));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.CursorError_OtherError<R>;
      return oth != null && object.Equals(this._err, oth._err);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 3;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._err));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Cursors.CursorError.OtherError";
      s += "(";
      s += Dafny.Helpers.ToString(this._err);
      s += ")";
      return s;
    }
  }

  public interface _ICursor__ {
    bool is_Cursor { get; }
    Dafny.ISequence<byte> dtor_s { get; }
    uint dtor_beg { get; }
    uint dtor_point { get; }
    uint dtor_end { get; }
    _ICursor__ DowncastClone();
    bool BOF_q { get; }
    bool EOF_q { get; }
    Dafny.ISequence<byte> Bytes();
    Std.JSON.Utils.Views.Core._IView__ Prefix();
    Std.JSON.Utils.Cursors._ICursor__ Suffix();
    Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> Split();
    uint PrefixLength();
    uint SuffixLength();
    uint Length();
    byte At(uint idx);
    byte SuffixAt(uint idx);
    short Peek();
    bool LookingAt(Dafny.Rune c);
    Std.JSON.Utils.Cursors._ICursor__ Skip(uint n);
    Std.JSON.Utils.Cursors._ICursor__ Unskip(uint n);
    Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> Get<__R>(__R err);
    Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertByte<__R>(byte b);
    Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertBytes<__R>(Dafny.ISequence<byte> bs, uint offset);
    Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertChar<__R>(Dafny.Rune c0);
    Std.JSON.Utils.Cursors._ICursor__ SkipByte();
    Std.JSON.Utils.Cursors._ICursor__ SkipIf(Func<byte, bool> p);
    Std.JSON.Utils.Cursors._ICursor__ SkipWhile(Func<byte, bool> p);
    Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> SkipWhileLexer<__A, __R>(Func<__A, short, Std.JSON.Utils.Lexers.Core._ILexerResult<__A, __R>> step, __A st);
  }
  public class Cursor__ : _ICursor__ {
    public readonly Dafny.ISequence<byte> _s;
    public readonly uint _beg;
    public readonly uint _point;
    public readonly uint _end;
    public Cursor__(Dafny.ISequence<byte> s, uint beg, uint point, uint end) {
      this._s = s;
      this._beg = beg;
      this._point = point;
      this._end = end;
    }
    public _ICursor__ DowncastClone() {
      if (this is _ICursor__ dt) { return dt; }
      return new Cursor__(_s, _beg, _point, _end);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Cursors.Cursor__;
      return oth != null && object.Equals(this._s, oth._s) && this._beg == oth._beg && this._point == oth._point && this._end == oth._end;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._s));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._beg));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._point));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._end));
      return (int) hash;
    }
    public override string ToString() {
      string ss = "Cursors.Cursor_.Cursor";
      ss += "(";
      ss += Dafny.Helpers.ToString(this._s);
      ss += ", ";
      ss += Dafny.Helpers.ToString(this._beg);
      ss += ", ";
      ss += Dafny.Helpers.ToString(this._point);
      ss += ", ";
      ss += Dafny.Helpers.ToString(this._end);
      ss += ")";
      return ss;
    }
    private static readonly Std.JSON.Utils.Cursors._ICursor__ theDefault = create(Dafny.Sequence<byte>.Empty, 0, 0, 0);
    public static Std.JSON.Utils.Cursors._ICursor__ Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__>(Std.JSON.Utils.Cursors.Cursor__.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Cursors._ICursor__> _TypeDescriptor() {
      return _TYPE;
    }
    public static _ICursor__ create(Dafny.ISequence<byte> s, uint beg, uint point, uint end) {
      return new Cursor__(s, beg, point, end);
    }
    public static _ICursor__ create_Cursor(Dafny.ISequence<byte> s, uint beg, uint point, uint end) {
      return create(s, beg, point, end);
    }
    public bool is_Cursor { get { return true; } }
    public Dafny.ISequence<byte> dtor_s {
      get {
        return this._s;
      }
    }
    public uint dtor_beg {
      get {
        return this._beg;
      }
    }
    public uint dtor_point {
      get {
        return this._point;
      }
    }
    public uint dtor_end {
      get {
        return this._end;
      }
    }
    public static Std.JSON.Utils.Cursors._ICursor__ OfView(Std.JSON.Utils.Views.Core._IView__ v) {
      return Std.JSON.Utils.Cursors.Cursor__.create((v).dtor_s, (v).dtor_beg, (v).dtor_beg, (v).dtor_end);
    }
    public static Std.JSON.Utils.Cursors._ICursor__ OfBytes(Dafny.ISequence<byte> bs) {
      return Std.JSON.Utils.Cursors.Cursor__.create(bs, 0U, 0U, (uint)(bs).LongCount);
    }
    public Dafny.ISequence<byte> Bytes() {
      return ((this).dtor_s).Subsequence((this).dtor_beg, (this).dtor_end);
    }
    public Std.JSON.Utils.Views.Core._IView__ Prefix() {
      return Std.JSON.Utils.Views.Core.View__.create((this).dtor_s, (this).dtor_beg, (this).dtor_point);
    }
    public Std.JSON.Utils.Cursors._ICursor__ Suffix() {
      Std.JSON.Utils.Cursors._ICursor__ _0_dt__update__tmp_h0 = this;
      uint _1_dt__update_hbeg_h0 = (this).dtor_point;
      return Std.JSON.Utils.Cursors.Cursor__.create((_0_dt__update__tmp_h0).dtor_s, _1_dt__update_hbeg_h0, (_0_dt__update__tmp_h0).dtor_point, (_0_dt__update__tmp_h0).dtor_end);
    }
    public Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> Split() {
      return Std.JSON.Utils.Cursors.Split<Std.JSON.Utils.Views.Core._IView__>.create((this).Prefix(), (this).Suffix());
    }
    public uint PrefixLength() {
      return ((this).dtor_point) - ((this).dtor_beg);
    }
    public uint SuffixLength() {
      return ((this).dtor_end) - ((this).dtor_point);
    }
    public uint Length() {
      return ((this).dtor_end) - ((this).dtor_beg);
    }
    public byte At(uint idx) {
      return ((this).dtor_s).Select(((this).dtor_beg) + (idx));
    }
    public byte SuffixAt(uint idx) {
      return ((this).dtor_s).Select(((this).dtor_point) + (idx));
    }
    public short Peek() {
      if ((this).EOF_q) {
        return (short)(-1);
      } else {
        return (short)((this).SuffixAt(0U));
      }
    }
    public bool LookingAt(Dafny.Rune c) {
      return ((this).Peek()) == ((short)((c).Value));
    }
    public Std.JSON.Utils.Cursors._ICursor__ Skip(uint n) {
      Std.JSON.Utils.Cursors._ICursor__ _0_dt__update__tmp_h0 = this;
      uint _1_dt__update_hpoint_h0 = ((this).dtor_point) + (n);
      return Std.JSON.Utils.Cursors.Cursor__.create((_0_dt__update__tmp_h0).dtor_s, (_0_dt__update__tmp_h0).dtor_beg, _1_dt__update_hpoint_h0, (_0_dt__update__tmp_h0).dtor_end);
    }
    public Std.JSON.Utils.Cursors._ICursor__ Unskip(uint n) {
      Std.JSON.Utils.Cursors._ICursor__ _0_dt__update__tmp_h0 = this;
      uint _1_dt__update_hpoint_h0 = ((this).dtor_point) - (n);
      return Std.JSON.Utils.Cursors.Cursor__.create((_0_dt__update__tmp_h0).dtor_s, (_0_dt__update__tmp_h0).dtor_beg, _1_dt__update_hpoint_h0, (_0_dt__update__tmp_h0).dtor_end);
    }
    public Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> Get<__R>(__R err) {
      if ((this).EOF_q) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_OtherError(err));
      } else {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Success((this).Skip(1U));
      }
    }
    public Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertByte<__R>(byte b) {
      short _0_nxt = (this).Peek();
      if ((_0_nxt) == ((short)(b))) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Success((this).Skip(1U));
      } else {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_ExpectingByte(b, _0_nxt));
      }
    }
    public Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertBytes<__R>(Dafny.ISequence<byte> bs, uint offset)
    {
      _ICursor__ _this = this;
    TAIL_CALL_START: ;
      if ((offset) == ((uint)(bs).LongCount)) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Success(_this);
      } else {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> _0_valueOrError0 = (_this).AssertByte<__R>((bs).Select(offset));
        if ((_0_valueOrError0).IsFailure()) {
          return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ICursor__>();
        } else {
          Std.JSON.Utils.Cursors._ICursor__ _1_ps = (_0_valueOrError0).Extract();
          Std.JSON.Utils.Cursors._ICursor__ _in0 = _1_ps;
          Dafny.ISequence<byte> _in1 = bs;
          uint _in2 = (offset) + (1U);
          _this = _in0;
          ;
          bs = _in1;
          offset = _in2;
          goto TAIL_CALL_START;
        }
      }
    }
    public Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> AssertChar<__R>(Dafny.Rune c0) {
      return (this).AssertByte<__R>((byte)((c0).Value));
    }
    public Std.JSON.Utils.Cursors._ICursor__ SkipByte() {
      if ((this).EOF_q) {
        return this;
      } else {
        return (this).Skip(1U);
      }
    }
    public Std.JSON.Utils.Cursors._ICursor__ SkipIf(Func<byte, bool> p) {
      if (((this).EOF_q) || (!(Dafny.Helpers.Id<Func<byte, bool>>(p)((this).SuffixAt(0U))))) {
        return this;
      } else {
        return (this).Skip(1U);
      }
    }
    public Std.JSON.Utils.Cursors._ICursor__ SkipWhile(Func<byte, bool> p)
    {
      Std.JSON.Utils.Cursors._ICursor__ ps = Std.JSON.Utils.Cursors.Cursor.Default();
      uint _0_point_k;
      _0_point_k = (this).dtor_point;
      uint _1_end;
      _1_end = (this).dtor_end;
      while (((_0_point_k) < (_1_end)) && (Dafny.Helpers.Id<Func<byte, bool>>(p)(((this).dtor_s).Select(_0_point_k)))) {
        _0_point_k = (_0_point_k) + (1U);
      }
      ps = Std.JSON.Utils.Cursors.Cursor__.create((this).dtor_s, (this).dtor_beg, _0_point_k, (this).dtor_end);
      return ps;
      return ps;
    }
    public Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> SkipWhileLexer<__A, __R>(Func<__A, short, Std.JSON.Utils.Lexers.Core._ILexerResult<__A, __R>> step, __A st)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>> pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.Default(Std.JSON.Utils.Cursors.Cursor.Default());
      uint _0_point_k;
      _0_point_k = (this).dtor_point;
      uint _1_end;
      _1_end = (this).dtor_end;
      __A _2_st_k;
      _2_st_k = st;
      while (true) {
        bool _3_eof;
        _3_eof = (_0_point_k) == (_1_end);
        short _4_minusone;
        _4_minusone = (short)(-1);
        short _5_c;
        if (_3_eof) {
          _5_c = _4_minusone;
        } else {
          _5_c = (short)(((this).dtor_s).Select(_0_point_k));
        }
        Std.JSON.Utils.Lexers.Core._ILexerResult<__A, __R> _source0 = Dafny.Helpers.Id<Func<__A, short, Std.JSON.Utils.Lexers.Core._ILexerResult<__A, __R>>>(step)(_2_st_k, _5_c);
        {
          if (_source0.is_Accept) {
            pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Success(Std.JSON.Utils.Cursors.Cursor__.create((this).dtor_s, (this).dtor_beg, _0_point_k, (this).dtor_end));
            return pr;
            goto after_match0;
          }
        }
        {
          if (_source0.is_Reject) {
            __R _6_err = _source0.dtor_err;
            pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_OtherError(_6_err));
            return pr;
            goto after_match0;
          }
        }
        {
          __A _7_st_k_k = _source0.dtor_st;
          if (_3_eof) {
            pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_EOF());
            return pr;
          } else {
            _2_st_k = _7_st_k_k;
            _0_point_k = (_0_point_k) + (1U);
          }
        }
      after_match0: ;
      }
      return pr;
    }
    public bool BOF_q { get {
      return ((this).dtor_point) == ((this).dtor_beg);
    } }
    public bool EOF_q { get {
      return ((this).dtor_point) == ((this).dtor_end);
    } }
  }
} // end of namespace Std.JSON.Utils.Cursors
namespace Std.JSON.Utils.Parsers {

  public partial class __default {
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>> ParserWitness<__T, __R>() {
      return ((System.Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>)((_0___v9) => {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_EOF());
      }));
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>> SubParserWitness<__T, __R>() {
      return ((System.Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>)((_0_cs) => {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<__R>.create_EOF());
      }));
    }
  }

  public partial class Parser<T, R> {
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> Default() {
      return Std.JSON.Utils.Parsers.__default.ParserWitness<T, R>();
    }
    public static Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T, Dafny.TypeDescriptor<R> _td_R) {
      return new Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>>(Std.JSON.Utils.Parsers.Parser<T, R>.Default());
    }
  }

  public interface _IParser__<T, out R> {
    bool is_Parser { get; }
    Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> dtor_fn { get; }
  }
  public class Parser__<T, R> : _IParser__<T, R> {
    public readonly Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> _fn;
    public Parser__(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      this._fn = fn;
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>> DowncastClone<__T, __R>(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> _this, Func<T, __T> converter0, Func<R, __R> converter1) {
      return (_this).DowncastClone<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>, Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>(Dafny.Helpers.Id<Std.JSON.Utils.Cursors._ICursor__>, Dafny.Helpers.CastConverter<Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Parsers.Parser__<T, R>;
      return oth != null && object.Equals(this._fn, oth._fn);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._fn));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Parsers.Parser_.Parser";
      s += "(";
      s += Dafny.Helpers.ToString(this._fn);
      s += ")";
      return s;
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> Default(T _default_T) {
      return ((Std.JSON.Utils.Cursors._ICursor__ x0) => Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>.Default(Std.JSON.Utils.Cursors.Split<T>.Default(_default_T)));
    }
    public static Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T) {
      return new Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>>(((Std.JSON.Utils.Cursors._ICursor__ x1) => Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>.Default(Std.JSON.Utils.Cursors.Split<T>.Default(_td_T.Default()))));
    }
    public static _IParser__<T, R> create(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      return new Parser__<T, R>(fn);
    }
    public static _IParser__<T, R> create_Parser(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      return create(fn);
    }
    public bool is_Parser { get { return true; } }
    public Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> dtor_fn {
      get {
        return this._fn;
      }
    }
  }

  public interface _ISubParser__<T, out R> {
    bool is_SubParser { get; }
    Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> dtor_fn { get; }
  }
  public class SubParser__<T, R> : _ISubParser__<T, R> {
    public readonly Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> _fn;
    public SubParser__(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      this._fn = fn;
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>> DowncastClone<__T, __R>(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> _this, Func<T, __T> converter0, Func<R, __R> converter1) {
      return (_this).DowncastClone<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>, Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>(Dafny.Helpers.Id<Std.JSON.Utils.Cursors._ICursor__>, Dafny.Helpers.CastConverter<Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<__R>>>);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Utils.Parsers.SubParser__<T, R>;
      return oth != null && object.Equals(this._fn, oth._fn);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._fn));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Parsers.SubParser_.SubParser";
      s += "(";
      s += Dafny.Helpers.ToString(this._fn);
      s += ")";
      return s;
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> Default() {
      return ((Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>)null);
    }
    public static Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>>(((Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>)null));
    }
    public static _ISubParser__<T, R> create(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      return new SubParser__<T, R>(fn);
    }
    public static _ISubParser__<T, R> create_SubParser(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> fn) {
      return create(fn);
    }
    public bool is_SubParser { get { return true; } }
    public Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> dtor_fn {
      get {
        return this._fn;
      }
    }
  }

  public partial class SubParser<T, R> {
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>> Default() {
      return Std.JSON.Utils.Parsers.__default.SubParserWitness<T, R>();
    }
    public static Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T, Dafny.TypeDescriptor<R> _td_R) {
      return new Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<T>, Std.JSON.Utils.Cursors._ICursorError<R>>>>(Std.JSON.Utils.Parsers.SubParser<T, R>.Default());
    }
  }
} // end of namespace Std.JSON.Utils.Parsers
namespace Std.JSON.Grammar {

  public partial class __default {
    public static bool Blank_q(byte b) {
      return ((((b) == ((byte)(32))) || ((b) == ((byte)(9)))) || ((b) == ((byte)(10)))) || ((b) == ((byte)(13)));
    }
    public static bool Digit_q(byte b) {
      return (((byte)((new Dafny.Rune('0')).Value)) <= (b)) && ((b) <= ((byte)((new Dafny.Rune('9')).Value)));
    }
    public static Dafny.ISequence<byte> NULL { get {
      return Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('n')).Value), (byte)((new Dafny.Rune('u')).Value), (byte)((new Dafny.Rune('l')).Value), (byte)((new Dafny.Rune('l')).Value));
    } }
    public static Dafny.ISequence<byte> TRUE { get {
      return Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('t')).Value), (byte)((new Dafny.Rune('r')).Value), (byte)((new Dafny.Rune('u')).Value), (byte)((new Dafny.Rune('e')).Value));
    } }
    public static Dafny.ISequence<byte> FALSE { get {
      return Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('f')).Value), (byte)((new Dafny.Rune('a')).Value), (byte)((new Dafny.Rune('l')).Value), (byte)((new Dafny.Rune('s')).Value), (byte)((new Dafny.Rune('e')).Value));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ DOUBLEQUOTE { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('\"')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ PERIOD { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('.')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ E { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('e')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ COLON { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune(':')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ COMMA { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune(',')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ LBRACE { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('{')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ RBRACE { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('}')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ LBRACKET { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('[')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ RBRACKET { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune(']')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ MINUS { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('-')).Value)));
    } }
    public static Std.JSON.Utils.Views.Core._IView__ EMPTY { get {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements());
    } }
  }

  public partial class jchar {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('b')).Value)));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jchar.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jquote {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.DOUBLEQUOTE;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jquote.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jperiod {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.PERIOD;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jperiod.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class je {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.E;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.je.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jcolon {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.COLON;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jcolon.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jcomma {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.COMMA;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jcomma.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jlbrace {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.LBRACE;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jlbrace.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jrbrace {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.RBRACE;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jrbrace.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jlbracket {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.LBRACKET;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jlbracket.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jrbracket {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.RBRACKET;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jrbracket.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jminus {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.MINUS;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jminus.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jsign {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Grammar.__default.EMPTY;
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jsign.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jblanks {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements());
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jblanks.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public interface _IStructural<out T> {
    bool is_Structural { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_before { get; }
    T dtor_t { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_after { get; }
    _IStructural<__T> DowncastClone<__T>(Func<T, __T> converter0);
  }
  public class Structural<T> : _IStructural<T> {
    public readonly Std.JSON.Utils.Views.Core._IView__ _before;
    public readonly T _t;
    public readonly Std.JSON.Utils.Views.Core._IView__ _after;
    public Structural(Std.JSON.Utils.Views.Core._IView__ before, T t, Std.JSON.Utils.Views.Core._IView__ after) {
      this._before = before;
      this._t = t;
      this._after = after;
    }
    public _IStructural<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _IStructural<__T> dt) { return dt; }
      return new Structural<__T>(_before, converter0(_t), _after);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Structural<T>;
      return oth != null && object.Equals(this._before, oth._before) && object.Equals(this._t, oth._t) && object.Equals(this._after, oth._after);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._before));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._t));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._after));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Structural.Structural";
      s += "(";
      s += Dafny.Helpers.ToString(this._before);
      s += ", ";
      s += Dafny.Helpers.ToString(this._t);
      s += ", ";
      s += Dafny.Helpers.ToString(this._after);
      s += ")";
      return s;
    }
    public static Std.JSON.Grammar._IStructural<T> Default(T _default_T) {
      return create(Std.JSON.Grammar.jblanks.Default(), _default_T, Std.JSON.Grammar.jblanks.Default());
    }
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._IStructural<T>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T) {
      return new Dafny.TypeDescriptor<Std.JSON.Grammar._IStructural<T>>(Std.JSON.Grammar.Structural<T>.Default(_td_T.Default()));
    }
    public static _IStructural<T> create(Std.JSON.Utils.Views.Core._IView__ before, T t, Std.JSON.Utils.Views.Core._IView__ after) {
      return new Structural<T>(before, t, after);
    }
    public static _IStructural<T> create_Structural(Std.JSON.Utils.Views.Core._IView__ before, T t, Std.JSON.Utils.Views.Core._IView__ after) {
      return create(before, t, after);
    }
    public bool is_Structural { get { return true; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_before {
      get {
        return this._before;
      }
    }
    public T dtor_t {
      get {
        return this._t;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_after {
      get {
        return this._after;
      }
    }
  }

  public interface _IMaybe<out T> {
    bool is_Empty { get; }
    bool is_NonEmpty { get; }
    T dtor_t { get; }
    _IMaybe<__T> DowncastClone<__T>(Func<T, __T> converter0);
  }
  public abstract class Maybe<T> : _IMaybe<T> {
    public Maybe() {
    }
    public static Std.JSON.Grammar._IMaybe<T> Default() {
      return create_Empty();
    }
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._IMaybe<T>> _TypeDescriptor() {
      return new Dafny.TypeDescriptor<Std.JSON.Grammar._IMaybe<T>>(Std.JSON.Grammar.Maybe<T>.Default());
    }
    public static _IMaybe<T> create_Empty() {
      return new Maybe_Empty<T>();
    }
    public static _IMaybe<T> create_NonEmpty(T t) {
      return new Maybe_NonEmpty<T>(t);
    }
    public bool is_Empty { get { return this is Maybe_Empty<T>; } }
    public bool is_NonEmpty { get { return this is Maybe_NonEmpty<T>; } }
    public T dtor_t {
      get {
        var d = this;
        return ((Maybe_NonEmpty<T>)d)._t;
      }
    }
    public abstract _IMaybe<__T> DowncastClone<__T>(Func<T, __T> converter0);
  }
  public class Maybe_Empty<T> : Maybe<T> {
    public Maybe_Empty() : base() {
    }
    public override _IMaybe<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _IMaybe<__T> dt) { return dt; }
      return new Maybe_Empty<__T>();
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Maybe_Empty<T>;
      return oth != null;
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Maybe.Empty";
      return s;
    }
  }
  public class Maybe_NonEmpty<T> : Maybe<T> {
    public readonly T _t;
    public Maybe_NonEmpty(T t) : base() {
      this._t = t;
    }
    public override _IMaybe<__T> DowncastClone<__T>(Func<T, __T> converter0) {
      if (this is _IMaybe<__T> dt) { return dt; }
      return new Maybe_NonEmpty<__T>(converter0(_t));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Maybe_NonEmpty<T>;
      return oth != null && object.Equals(this._t, oth._t);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._t));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Maybe.NonEmpty";
      s += "(";
      s += Dafny.Helpers.ToString(this._t);
      s += ")";
      return s;
    }
  }

  public interface _ISuffixed<out T, out S> {
    bool is_Suffixed { get; }
    T dtor_t { get; }
    Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> dtor_suffix { get; }
    _ISuffixed<__T, __S> DowncastClone<__T, __S>(Func<T, __T> converter0, Func<S, __S> converter1);
  }
  public class Suffixed<T, S> : _ISuffixed<T, S> {
    public readonly T _t;
    public readonly Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> _suffix;
    public Suffixed(T t, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> suffix) {
      this._t = t;
      this._suffix = suffix;
    }
    public _ISuffixed<__T, __S> DowncastClone<__T, __S>(Func<T, __T> converter0, Func<S, __S> converter1) {
      if (this is _ISuffixed<__T, __S> dt) { return dt; }
      return new Suffixed<__T, __S>(converter0(_t), (_suffix).DowncastClone<Std.JSON.Grammar._IStructural<__S>>(Dafny.Helpers.CastConverter<Std.JSON.Grammar._IStructural<S>, Std.JSON.Grammar._IStructural<__S>>));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Suffixed<T, S>;
      return oth != null && object.Equals(this._t, oth._t) && object.Equals(this._suffix, oth._suffix);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._t));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._suffix));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Suffixed.Suffixed";
      s += "(";
      s += Dafny.Helpers.ToString(this._t);
      s += ", ";
      s += Dafny.Helpers.ToString(this._suffix);
      s += ")";
      return s;
    }
    public static Std.JSON.Grammar._ISuffixed<T, S> Default(T _default_T) {
      return create(_default_T, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<S>>.Default());
    }
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._ISuffixed<T, S>> _TypeDescriptor(Dafny.TypeDescriptor<T> _td_T) {
      return new Dafny.TypeDescriptor<Std.JSON.Grammar._ISuffixed<T, S>>(Std.JSON.Grammar.Suffixed<T, S>.Default(_td_T.Default()));
    }
    public static _ISuffixed<T, S> create(T t, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> suffix) {
      return new Suffixed<T, S>(t, suffix);
    }
    public static _ISuffixed<T, S> create_Suffixed(T t, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> suffix) {
      return create(t, suffix);
    }
    public bool is_Suffixed { get { return true; } }
    public T dtor_t {
      get {
        return this._t;
      }
    }
    public Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<S>> dtor_suffix {
      get {
        return this._suffix;
      }
    }
  }

  public partial class SuffixedSequence<D, S> {
    public static Dafny.TypeDescriptor<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>>> _TypeDescriptor(Dafny.TypeDescriptor<D> _td_D, Dafny.TypeDescriptor<S> _td_S) {
      return new Dafny.TypeDescriptor<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>>>(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<D, S>>.Empty);
    }
  }

  public interface _IBracketed<out L, out D, out S, out R> {
    bool is_Bracketed { get; }
    Std.JSON.Grammar._IStructural<L> dtor_l { get; }
    Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> dtor_data { get; }
    Std.JSON.Grammar._IStructural<R> dtor_r { get; }
    _IBracketed<__L, __D, __S, __R> DowncastClone<__L, __D, __S, __R>(Func<L, __L> converter0, Func<D, __D> converter1, Func<S, __S> converter2, Func<R, __R> converter3);
  }
  public class Bracketed<L, D, S, R> : _IBracketed<L, D, S, R> {
    public readonly Std.JSON.Grammar._IStructural<L> _l;
    public readonly Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> _data;
    public readonly Std.JSON.Grammar._IStructural<R> _r;
    public Bracketed(Std.JSON.Grammar._IStructural<L> l, Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> data, Std.JSON.Grammar._IStructural<R> r) {
      this._l = l;
      this._data = data;
      this._r = r;
    }
    public _IBracketed<__L, __D, __S, __R> DowncastClone<__L, __D, __S, __R>(Func<L, __L> converter0, Func<D, __D> converter1, Func<S, __S> converter2, Func<R, __R> converter3) {
      if (this is _IBracketed<__L, __D, __S, __R> dt) { return dt; }
      return new Bracketed<__L, __D, __S, __R>((_l).DowncastClone<__L>(Dafny.Helpers.CastConverter<L, __L>), (_data).DowncastClone<Std.JSON.Grammar._ISuffixed<__D, __S>>(Dafny.Helpers.CastConverter<Std.JSON.Grammar._ISuffixed<D, S>, Std.JSON.Grammar._ISuffixed<__D, __S>>), (_r).DowncastClone<__R>(Dafny.Helpers.CastConverter<R, __R>));
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Bracketed<L, D, S, R>;
      return oth != null && object.Equals(this._l, oth._l) && object.Equals(this._data, oth._data) && object.Equals(this._r, oth._r);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._l));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._data));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._r));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Bracketed.Bracketed";
      s += "(";
      s += Dafny.Helpers.ToString(this._l);
      s += ", ";
      s += Dafny.Helpers.ToString(this._data);
      s += ", ";
      s += Dafny.Helpers.ToString(this._r);
      s += ")";
      return s;
    }
    public static Std.JSON.Grammar._IBracketed<L, D, S, R> Default(L _default_L, R _default_R) {
      return create(Std.JSON.Grammar.Structural<L>.Default(_default_L), Dafny.Sequence<Std.JSON.Grammar._ISuffixed<D, S>>.Empty, Std.JSON.Grammar.Structural<R>.Default(_default_R));
    }
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._IBracketed<L, D, S, R>> _TypeDescriptor(Dafny.TypeDescriptor<L> _td_L, Dafny.TypeDescriptor<R> _td_R) {
      return new Dafny.TypeDescriptor<Std.JSON.Grammar._IBracketed<L, D, S, R>>(Std.JSON.Grammar.Bracketed<L, D, S, R>.Default(_td_L.Default(), _td_R.Default()));
    }
    public static _IBracketed<L, D, S, R> create(Std.JSON.Grammar._IStructural<L> l, Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> data, Std.JSON.Grammar._IStructural<R> r) {
      return new Bracketed<L, D, S, R>(l, data, r);
    }
    public static _IBracketed<L, D, S, R> create_Bracketed(Std.JSON.Grammar._IStructural<L> l, Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> data, Std.JSON.Grammar._IStructural<R> r) {
      return create(l, data, r);
    }
    public bool is_Bracketed { get { return true; } }
    public Std.JSON.Grammar._IStructural<L> dtor_l {
      get {
        return this._l;
      }
    }
    public Dafny.ISequence<Std.JSON.Grammar._ISuffixed<D, S>> dtor_data {
      get {
        return this._data;
      }
    }
    public Std.JSON.Grammar._IStructural<R> dtor_r {
      get {
        return this._r;
      }
    }
  }

  public partial class jnull {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Std.JSON.Grammar.__default.NULL);
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jnull.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jbool {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Std.JSON.Grammar.__default.TRUE);
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jbool.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jdigits {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements());
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jdigits.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jnum {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('0')).Value)));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jnum.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jint {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('0')).Value)));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jint.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jstr {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements());
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.jstr.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public interface _Ijstring {
    bool is_JString { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_lq { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_contents { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_rq { get; }
    _Ijstring DowncastClone();
  }
  public class jstring : _Ijstring {
    public readonly Std.JSON.Utils.Views.Core._IView__ _lq;
    public readonly Std.JSON.Utils.Views.Core._IView__ _contents;
    public readonly Std.JSON.Utils.Views.Core._IView__ _rq;
    public jstring(Std.JSON.Utils.Views.Core._IView__ lq, Std.JSON.Utils.Views.Core._IView__ contents, Std.JSON.Utils.Views.Core._IView__ rq) {
      this._lq = lq;
      this._contents = contents;
      this._rq = rq;
    }
    public _Ijstring DowncastClone() {
      if (this is _Ijstring dt) { return dt; }
      return new jstring(_lq, _contents, _rq);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.jstring;
      return oth != null && object.Equals(this._lq, oth._lq) && object.Equals(this._contents, oth._contents) && object.Equals(this._rq, oth._rq);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._lq));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._contents));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._rq));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.jstring.JString";
      s += "(";
      s += Dafny.Helpers.ToString(this._lq);
      s += ", ";
      s += Dafny.Helpers.ToString(this._contents);
      s += ", ";
      s += Dafny.Helpers.ToString(this._rq);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Grammar._Ijstring theDefault = create(Std.JSON.Grammar.jquote.Default(), Std.JSON.Grammar.jstr.Default(), Std.JSON.Grammar.jquote.Default());
    public static Std.JSON.Grammar._Ijstring Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._Ijstring> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._Ijstring>(Std.JSON.Grammar.jstring.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._Ijstring> _TypeDescriptor() {
      return _TYPE;
    }
    public static _Ijstring create(Std.JSON.Utils.Views.Core._IView__ lq, Std.JSON.Utils.Views.Core._IView__ contents, Std.JSON.Utils.Views.Core._IView__ rq) {
      return new jstring(lq, contents, rq);
    }
    public static _Ijstring create_JString(Std.JSON.Utils.Views.Core._IView__ lq, Std.JSON.Utils.Views.Core._IView__ contents, Std.JSON.Utils.Views.Core._IView__ rq) {
      return create(lq, contents, rq);
    }
    public bool is_JString { get { return true; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_lq {
      get {
        return this._lq;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_contents {
      get {
        return this._contents;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_rq {
      get {
        return this._rq;
      }
    }
  }

  public interface _IjKeyValue {
    bool is_KeyValue { get; }
    Std.JSON.Grammar._Ijstring dtor_k { get; }
    Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> dtor_colon { get; }
    Std.JSON.Grammar._IValue dtor_v { get; }
    _IjKeyValue DowncastClone();
  }
  public class jKeyValue : _IjKeyValue {
    public readonly Std.JSON.Grammar._Ijstring _k;
    public readonly Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> _colon;
    public readonly Std.JSON.Grammar._IValue _v;
    public jKeyValue(Std.JSON.Grammar._Ijstring k, Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> colon, Std.JSON.Grammar._IValue v) {
      this._k = k;
      this._colon = colon;
      this._v = v;
    }
    public _IjKeyValue DowncastClone() {
      if (this is _IjKeyValue dt) { return dt; }
      return new jKeyValue(_k, _colon, _v);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.jKeyValue;
      return oth != null && object.Equals(this._k, oth._k) && object.Equals(this._colon, oth._colon) && object.Equals(this._v, oth._v);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._k));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._colon));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._v));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.jKeyValue.KeyValue";
      s += "(";
      s += Dafny.Helpers.ToString(this._k);
      s += ", ";
      s += Dafny.Helpers.ToString(this._colon);
      s += ", ";
      s += Dafny.Helpers.ToString(this._v);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Grammar._IjKeyValue theDefault = create(Std.JSON.Grammar.jstring.Default(), Std.JSON.Grammar.Structural<Std.JSON.Utils.Views.Core._IView__>.Default(Std.JSON.Grammar.jcolon.Default()), Std.JSON.Grammar.Value.Default());
    public static Std.JSON.Grammar._IjKeyValue Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._IjKeyValue> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._IjKeyValue>(Std.JSON.Grammar.jKeyValue.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._IjKeyValue> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IjKeyValue create(Std.JSON.Grammar._Ijstring k, Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> colon, Std.JSON.Grammar._IValue v) {
      return new jKeyValue(k, colon, v);
    }
    public static _IjKeyValue create_KeyValue(Std.JSON.Grammar._Ijstring k, Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> colon, Std.JSON.Grammar._IValue v) {
      return create(k, colon, v);
    }
    public bool is_KeyValue { get { return true; } }
    public Std.JSON.Grammar._Ijstring dtor_k {
      get {
        return this._k;
      }
    }
    public Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> dtor_colon {
      get {
        return this._colon;
      }
    }
    public Std.JSON.Grammar._IValue dtor_v {
      get {
        return this._v;
      }
    }
  }

  public interface _Ijfrac {
    bool is_JFrac { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_period { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_num { get; }
    _Ijfrac DowncastClone();
  }
  public class jfrac : _Ijfrac {
    public readonly Std.JSON.Utils.Views.Core._IView__ _period;
    public readonly Std.JSON.Utils.Views.Core._IView__ _num;
    public jfrac(Std.JSON.Utils.Views.Core._IView__ period, Std.JSON.Utils.Views.Core._IView__ num) {
      this._period = period;
      this._num = num;
    }
    public _Ijfrac DowncastClone() {
      if (this is _Ijfrac dt) { return dt; }
      return new jfrac(_period, _num);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.jfrac;
      return oth != null && object.Equals(this._period, oth._period) && object.Equals(this._num, oth._num);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._period));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._num));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.jfrac.JFrac";
      s += "(";
      s += Dafny.Helpers.ToString(this._period);
      s += ", ";
      s += Dafny.Helpers.ToString(this._num);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Grammar._Ijfrac theDefault = create(Std.JSON.Grammar.jperiod.Default(), Std.JSON.Grammar.jnum.Default());
    public static Std.JSON.Grammar._Ijfrac Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._Ijfrac> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._Ijfrac>(Std.JSON.Grammar.jfrac.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._Ijfrac> _TypeDescriptor() {
      return _TYPE;
    }
    public static _Ijfrac create(Std.JSON.Utils.Views.Core._IView__ period, Std.JSON.Utils.Views.Core._IView__ num) {
      return new jfrac(period, num);
    }
    public static _Ijfrac create_JFrac(Std.JSON.Utils.Views.Core._IView__ period, Std.JSON.Utils.Views.Core._IView__ num) {
      return create(period, num);
    }
    public bool is_JFrac { get { return true; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_period {
      get {
        return this._period;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_num {
      get {
        return this._num;
      }
    }
  }

  public interface _Ijexp {
    bool is_JExp { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_e { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_sign { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_num { get; }
    _Ijexp DowncastClone();
  }
  public class jexp : _Ijexp {
    public readonly Std.JSON.Utils.Views.Core._IView__ _e;
    public readonly Std.JSON.Utils.Views.Core._IView__ _sign;
    public readonly Std.JSON.Utils.Views.Core._IView__ _num;
    public jexp(Std.JSON.Utils.Views.Core._IView__ e, Std.JSON.Utils.Views.Core._IView__ sign, Std.JSON.Utils.Views.Core._IView__ num) {
      this._e = e;
      this._sign = sign;
      this._num = num;
    }
    public _Ijexp DowncastClone() {
      if (this is _Ijexp dt) { return dt; }
      return new jexp(_e, _sign, _num);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.jexp;
      return oth != null && object.Equals(this._e, oth._e) && object.Equals(this._sign, oth._sign) && object.Equals(this._num, oth._num);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._e));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._sign));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._num));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.jexp.JExp";
      s += "(";
      s += Dafny.Helpers.ToString(this._e);
      s += ", ";
      s += Dafny.Helpers.ToString(this._sign);
      s += ", ";
      s += Dafny.Helpers.ToString(this._num);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Grammar._Ijexp theDefault = create(Std.JSON.Grammar.je.Default(), Std.JSON.Grammar.jsign.Default(), Std.JSON.Grammar.jnum.Default());
    public static Std.JSON.Grammar._Ijexp Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._Ijexp> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._Ijexp>(Std.JSON.Grammar.jexp.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._Ijexp> _TypeDescriptor() {
      return _TYPE;
    }
    public static _Ijexp create(Std.JSON.Utils.Views.Core._IView__ e, Std.JSON.Utils.Views.Core._IView__ sign, Std.JSON.Utils.Views.Core._IView__ num) {
      return new jexp(e, sign, num);
    }
    public static _Ijexp create_JExp(Std.JSON.Utils.Views.Core._IView__ e, Std.JSON.Utils.Views.Core._IView__ sign, Std.JSON.Utils.Views.Core._IView__ num) {
      return create(e, sign, num);
    }
    public bool is_JExp { get { return true; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_e {
      get {
        return this._e;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_sign {
      get {
        return this._sign;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_num {
      get {
        return this._num;
      }
    }
  }

  public interface _Ijnumber {
    bool is_JNumber { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_minus { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_num { get; }
    Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> dtor_frac { get; }
    Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> dtor_exp { get; }
    _Ijnumber DowncastClone();
  }
  public class jnumber : _Ijnumber {
    public readonly Std.JSON.Utils.Views.Core._IView__ _minus;
    public readonly Std.JSON.Utils.Views.Core._IView__ _num;
    public readonly Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> _frac;
    public readonly Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> _exp;
    public jnumber(Std.JSON.Utils.Views.Core._IView__ minus, Std.JSON.Utils.Views.Core._IView__ num, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> frac, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> exp) {
      this._minus = minus;
      this._num = num;
      this._frac = frac;
      this._exp = exp;
    }
    public _Ijnumber DowncastClone() {
      if (this is _Ijnumber dt) { return dt; }
      return new jnumber(_minus, _num, _frac, _exp);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.jnumber;
      return oth != null && object.Equals(this._minus, oth._minus) && object.Equals(this._num, oth._num) && object.Equals(this._frac, oth._frac) && object.Equals(this._exp, oth._exp);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._minus));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._num));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._frac));
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._exp));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.jnumber.JNumber";
      s += "(";
      s += Dafny.Helpers.ToString(this._minus);
      s += ", ";
      s += Dafny.Helpers.ToString(this._num);
      s += ", ";
      s += Dafny.Helpers.ToString(this._frac);
      s += ", ";
      s += Dafny.Helpers.ToString(this._exp);
      s += ")";
      return s;
    }
    private static readonly Std.JSON.Grammar._Ijnumber theDefault = create(Std.JSON.Grammar.jminus.Default(), Std.JSON.Grammar.jnum.Default(), Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijfrac>.Default(), Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijexp>.Default());
    public static Std.JSON.Grammar._Ijnumber Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._Ijnumber> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._Ijnumber>(Std.JSON.Grammar.jnumber.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._Ijnumber> _TypeDescriptor() {
      return _TYPE;
    }
    public static _Ijnumber create(Std.JSON.Utils.Views.Core._IView__ minus, Std.JSON.Utils.Views.Core._IView__ num, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> frac, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> exp) {
      return new jnumber(minus, num, frac, exp);
    }
    public static _Ijnumber create_JNumber(Std.JSON.Utils.Views.Core._IView__ minus, Std.JSON.Utils.Views.Core._IView__ num, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> frac, Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> exp) {
      return create(minus, num, frac, exp);
    }
    public bool is_JNumber { get { return true; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_minus {
      get {
        return this._minus;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_num {
      get {
        return this._num;
      }
    }
    public Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> dtor_frac {
      get {
        return this._frac;
      }
    }
    public Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> dtor_exp {
      get {
        return this._exp;
      }
    }
  }

  public interface _IValue {
    bool is_Null { get; }
    bool is_Bool { get; }
    bool is_String { get; }
    bool is_Number { get; }
    bool is_Object { get; }
    bool is_Array { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_n { get; }
    Std.JSON.Utils.Views.Core._IView__ dtor_b { get; }
    Std.JSON.Grammar._Ijstring dtor_str { get; }
    Std.JSON.Grammar._Ijnumber dtor_num { get; }
    Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> dtor_obj { get; }
    Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> dtor_arr { get; }
    _IValue DowncastClone();
  }
  public abstract class Value : _IValue {
    public Value() {
    }
    private static readonly Std.JSON.Grammar._IValue theDefault = create_Null(Std.JSON.Grammar.jnull.Default());
    public static Std.JSON.Grammar._IValue Default() {
      return theDefault;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Grammar._IValue> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Grammar._IValue>(Std.JSON.Grammar.Value.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Grammar._IValue> _TypeDescriptor() {
      return _TYPE;
    }
    public static _IValue create_Null(Std.JSON.Utils.Views.Core._IView__ n) {
      return new Value_Null(n);
    }
    public static _IValue create_Bool(Std.JSON.Utils.Views.Core._IView__ b) {
      return new Value_Bool(b);
    }
    public static _IValue create_String(Std.JSON.Grammar._Ijstring str) {
      return new Value_String(str);
    }
    public static _IValue create_Number(Std.JSON.Grammar._Ijnumber num) {
      return new Value_Number(num);
    }
    public static _IValue create_Object(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj) {
      return new Value_Object(obj);
    }
    public static _IValue create_Array(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr) {
      return new Value_Array(arr);
    }
    public bool is_Null { get { return this is Value_Null; } }
    public bool is_Bool { get { return this is Value_Bool; } }
    public bool is_String { get { return this is Value_String; } }
    public bool is_Number { get { return this is Value_Number; } }
    public bool is_Object { get { return this is Value_Object; } }
    public bool is_Array { get { return this is Value_Array; } }
    public Std.JSON.Utils.Views.Core._IView__ dtor_n {
      get {
        var d = this;
        return ((Value_Null)d)._n;
      }
    }
    public Std.JSON.Utils.Views.Core._IView__ dtor_b {
      get {
        var d = this;
        return ((Value_Bool)d)._b;
      }
    }
    public Std.JSON.Grammar._Ijstring dtor_str {
      get {
        var d = this;
        return ((Value_String)d)._str;
      }
    }
    public Std.JSON.Grammar._Ijnumber dtor_num {
      get {
        var d = this;
        return ((Value_Number)d)._num;
      }
    }
    public Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> dtor_obj {
      get {
        var d = this;
        return ((Value_Object)d)._obj;
      }
    }
    public Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> dtor_arr {
      get {
        var d = this;
        return ((Value_Array)d)._arr;
      }
    }
    public abstract _IValue DowncastClone();
  }
  public class Value_Null : Value {
    public readonly Std.JSON.Utils.Views.Core._IView__ _n;
    public Value_Null(Std.JSON.Utils.Views.Core._IView__ n) : base() {
      this._n = n;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_Null(_n);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_Null;
      return oth != null && object.Equals(this._n, oth._n);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 0;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._n));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.Null";
      s += "(";
      s += Dafny.Helpers.ToString(this._n);
      s += ")";
      return s;
    }
  }
  public class Value_Bool : Value {
    public readonly Std.JSON.Utils.Views.Core._IView__ _b;
    public Value_Bool(Std.JSON.Utils.Views.Core._IView__ b) : base() {
      this._b = b;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_Bool(_b);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_Bool;
      return oth != null && object.Equals(this._b, oth._b);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 1;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._b));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.Bool";
      s += "(";
      s += Dafny.Helpers.ToString(this._b);
      s += ")";
      return s;
    }
  }
  public class Value_String : Value {
    public readonly Std.JSON.Grammar._Ijstring _str;
    public Value_String(Std.JSON.Grammar._Ijstring str) : base() {
      this._str = str;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_String(_str);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_String;
      return oth != null && object.Equals(this._str, oth._str);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 2;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._str));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.String";
      s += "(";
      s += Dafny.Helpers.ToString(this._str);
      s += ")";
      return s;
    }
  }
  public class Value_Number : Value {
    public readonly Std.JSON.Grammar._Ijnumber _num;
    public Value_Number(Std.JSON.Grammar._Ijnumber num) : base() {
      this._num = num;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_Number(_num);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_Number;
      return oth != null && object.Equals(this._num, oth._num);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 3;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._num));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.Number";
      s += "(";
      s += Dafny.Helpers.ToString(this._num);
      s += ")";
      return s;
    }
  }
  public class Value_Object : Value {
    public readonly Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _obj;
    public Value_Object(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj) : base() {
      this._obj = obj;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_Object(_obj);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_Object;
      return oth != null && object.Equals(this._obj, oth._obj);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 4;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._obj));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.Object";
      s += "(";
      s += Dafny.Helpers.ToString(this._obj);
      s += ")";
      return s;
    }
  }
  public class Value_Array : Value {
    public readonly Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _arr;
    public Value_Array(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr) : base() {
      this._arr = arr;
    }
    public override _IValue DowncastClone() {
      if (this is _IValue dt) { return dt; }
      return new Value_Array(_arr);
    }
    public override bool Equals(object other) {
      var oth = other as Std.JSON.Grammar.Value_Array;
      return oth != null && object.Equals(this._arr, oth._arr);
    }
    public override int GetHashCode() {
      ulong hash = 5381;
      hash = ((hash << 5) + hash) + 5;
      hash = ((hash << 5) + hash) + ((ulong)Dafny.Helpers.GetHashCode(this._arr));
      return (int) hash;
    }
    public override string ToString() {
      string s = "Grammar.Value.Array";
      s += "(";
      s += Dafny.Helpers.ToString(this._arr);
      s += ")";
      return s;
    }
  }
} // end of namespace Std.JSON.Grammar
namespace Std.JSON.ByteStrConversion {

  public partial class __default {
    public static BigInteger BASE() {
      return Std.JSON.ByteStrConversion.__default.@base;
    }
    public static bool IsDigitChar(byte c) {
      return (Std.JSON.ByteStrConversion.__default.charToDigit).Contains(c);
    }
    public static Dafny.ISequence<byte> OfDigits(Dafny.ISequence<BigInteger> digits) {
      Dafny.ISequence<byte> _0___accumulator = Dafny.Sequence<byte>.FromElements();
    TAIL_CALL_START: ;
      if ((digits).Equals(Dafny.Sequence<BigInteger>.FromElements())) {
        return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.FromElements((Std.JSON.ByteStrConversion.__default.chars).Select((digits).Select(BigInteger.Zero))), _0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = (digits).Drop(BigInteger.One);
        digits = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<byte> OfNat(BigInteger n) {
      if ((n).Sign == 0) {
        return Dafny.Sequence<byte>.FromElements((Std.JSON.ByteStrConversion.__default.chars).Select(BigInteger.Zero));
      } else {
        return Std.JSON.ByteStrConversion.__default.OfDigits(Std.JSON.ByteStrConversion.__default.FromNat(n));
      }
    }
    public static bool IsNumberStr(Dafny.ISequence<byte> str, byte minus)
    {
      return !(!(str).Equals(Dafny.Sequence<byte>.FromElements())) || (((((str).Select(BigInteger.Zero)) == (minus)) || ((Std.JSON.ByteStrConversion.__default.charToDigit).Contains((str).Select(BigInteger.Zero)))) && (Dafny.Helpers.Id<Func<Dafny.ISequence<byte>, bool>>((_0_str) => Dafny.Helpers.Quantifier<byte>(((_0_str).Drop(BigInteger.One)).UniqueElements, true, (((_forall_var_0) => {
        byte _1_c = (byte)_forall_var_0;
        return !(((_0_str).Drop(BigInteger.One)).Contains(_1_c)) || (Std.JSON.ByteStrConversion.__default.IsDigitChar(_1_c));
      }))))(str)));
    }
    public static Dafny.ISequence<byte> OfInt(BigInteger n, byte minus)
    {
      if ((n).Sign != -1) {
        return Std.JSON.ByteStrConversion.__default.OfNat(n);
      } else {
        return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.FromElements(minus), Std.JSON.ByteStrConversion.__default.OfNat((BigInteger.Zero) - (n)));
      }
    }
    public static BigInteger ToNat(Dafny.ISequence<byte> str) {
      if ((str).Equals(Dafny.Sequence<byte>.FromElements())) {
        return BigInteger.Zero;
      } else {
        byte _0_c = (str).Select((new BigInteger((str).Count)) - (BigInteger.One));
        return ((Std.JSON.ByteStrConversion.__default.ToNat((str).Take((new BigInteger((str).Count)) - (BigInteger.One)))) * (Std.JSON.ByteStrConversion.__default.@base)) + (Dafny.Map<byte, BigInteger>.Select(Std.JSON.ByteStrConversion.__default.charToDigit,_0_c));
      }
    }
    public static BigInteger ToInt(Dafny.ISequence<byte> str, byte minus)
    {
      if (Dafny.Sequence<byte>.IsPrefixOf(Dafny.Sequence<byte>.FromElements(minus), str)) {
        return (BigInteger.Zero) - (Std.JSON.ByteStrConversion.__default.ToNat((str).Drop(BigInteger.One)));
      } else {
        return Std.JSON.ByteStrConversion.__default.ToNat(str);
      }
    }
    public static BigInteger ToNatRight(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return BigInteger.Zero;
      } else {
        return ((Std.JSON.ByteStrConversion.__default.ToNatRight(Std.Collections.Seq.__default.DropFirst<BigInteger>(xs))) * (Std.JSON.ByteStrConversion.__default.BASE())) + (Std.Collections.Seq.__default.First<BigInteger>(xs));
      }
    }
    public static BigInteger ToNatLeft(Dafny.ISequence<BigInteger> xs) {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) * (Std.Arithmetic.Power.__default.Pow(Std.JSON.ByteStrConversion.__default.BASE(), (new BigInteger((xs).Count)) - (BigInteger.One)))) + (_0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = Std.Collections.Seq.__default.DropLast<BigInteger>(xs);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> FromNat(BigInteger n) {
      Dafny.ISequence<BigInteger> _0___accumulator = Dafny.Sequence<BigInteger>.FromElements();
    TAIL_CALL_START: ;
      if ((n).Sign == 0) {
        return Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements(Dafny.Helpers.EuclideanModulus(n, Std.JSON.ByteStrConversion.__default.BASE())));
        BigInteger _in0 = Dafny.Helpers.EuclideanDivision(n, Std.JSON.ByteStrConversion.__default.BASE());
        n = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtend(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)) >= (n)) {
        return xs;
      } else {
        Dafny.ISequence<BigInteger> _in0 = Dafny.Sequence<BigInteger>.Concat(xs, Dafny.Sequence<BigInteger>.FromElements(BigInteger.Zero));
        BigInteger _in1 = n;
        xs = _in0;
        n = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtendMultiple(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
      BigInteger _0_newLen = ((new BigInteger((xs).Count)) + (n)) - (Dafny.Helpers.EuclideanModulus(new BigInteger((xs).Count), n));
      return Std.JSON.ByteStrConversion.__default.SeqExtend(xs, _0_newLen);
    }
    public static Dafny.ISequence<BigInteger> FromNatWithLen(BigInteger n, BigInteger len)
    {
      return Std.JSON.ByteStrConversion.__default.SeqExtend(Std.JSON.ByteStrConversion.__default.FromNat(n), len);
    }
    public static Dafny.ISequence<BigInteger> SeqZero(BigInteger len) {
      Dafny.ISequence<BigInteger> _0_xs = Std.JSON.ByteStrConversion.__default.FromNatWithLen(BigInteger.Zero, len);
      return _0_xs;
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqAdd(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.JSON.ByteStrConversion.__default.SeqAdd(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs_k = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        BigInteger _2_sum = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) + (Std.Collections.Seq.__default.Last<BigInteger>(ys))) + (_1_cin);
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((_2_sum) < (Std.JSON.ByteStrConversion.__default.BASE())) ? (_System.Tuple2<BigInteger, BigInteger>.create(_2_sum, BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((_2_sum) - (Std.JSON.ByteStrConversion.__default.BASE()), BigInteger.One)));
        BigInteger _3_sum__out = _let_tmp_rhs1.dtor__0;
        BigInteger _4_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs_k, Dafny.Sequence<BigInteger>.FromElements(_3_sum__out)), _4_cout);
      }
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqSub(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.JSON.ByteStrConversion.__default.SeqSub(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((Std.Collections.Seq.__default.Last<BigInteger>(xs)) >= ((Std.Collections.Seq.__default.Last<BigInteger>(ys)) + (_1_cin))) ? (_System.Tuple2<BigInteger, BigInteger>.create(((Std.Collections.Seq.__default.Last<BigInteger>(xs)) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((((Std.JSON.ByteStrConversion.__default.BASE()) + (Std.Collections.Seq.__default.Last<BigInteger>(xs))) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.One)));
        BigInteger _2_diff__out = _let_tmp_rhs1.dtor__0;
        BigInteger _3_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs, Dafny.Sequence<BigInteger>.FromElements(_2_diff__out)), _3_cout);
      }
    }
    public static Dafny.ISequence<byte> chars { get {
      return Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('0')).Value), (byte)((new Dafny.Rune('1')).Value), (byte)((new Dafny.Rune('2')).Value), (byte)((new Dafny.Rune('3')).Value), (byte)((new Dafny.Rune('4')).Value), (byte)((new Dafny.Rune('5')).Value), (byte)((new Dafny.Rune('6')).Value), (byte)((new Dafny.Rune('7')).Value), (byte)((new Dafny.Rune('8')).Value), (byte)((new Dafny.Rune('9')).Value));
    } }
    public static BigInteger @base { get {
      return new BigInteger((Std.JSON.ByteStrConversion.__default.chars).Count);
    } }
    public static Dafny.IMap<byte,BigInteger> charToDigit { get {
      return Dafny.Map<byte, BigInteger>.FromElements(new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('0')).Value), BigInteger.Zero), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('1')).Value), BigInteger.One), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('2')).Value), new BigInteger(2)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('3')).Value), new BigInteger(3)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('4')).Value), new BigInteger(4)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('5')).Value), new BigInteger(5)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('6')).Value), new BigInteger(6)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('7')).Value), new BigInteger(7)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('8')).Value), new BigInteger(8)), new Dafny.Pair<byte, BigInteger>((byte)((new Dafny.Rune('9')).Value), new BigInteger(9)));
    } }
  }

  public partial class CharSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<byte>>(Dafny.Sequence<byte>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<byte> __source) {
      Dafny.ISequence<byte> _0_chars = __source;
      return (new BigInteger((_0_chars).Count)) > (BigInteger.One);
    }
  }

  public partial class digit {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _1_i = __source;
      if (_System.nat._Is(_1_i)) {
        return ((_1_i).Sign != -1) && ((_1_i) < (Std.JSON.ByteStrConversion.__default.BASE()));
      }
      return false;
    }
  }
} // end of namespace Std.JSON.ByteStrConversion
namespace Std.JSON.Serializer {

  public partial class __default {
    public static Std.JSON.Utils.Views.Core._IView__ Bool(bool b) {
      return Std.JSON.Utils.Views.Core.View__.OfBytes(((b) ? (Std.JSON.Grammar.__default.TRUE) : (Std.JSON.Grammar.__default.FALSE)));
    }
    public static Std.Wrappers._IOutcome<Std.JSON.Errors._ISerializationError> CheckLength<__T>(Dafny.ISequence<__T> s, Std.JSON.Errors._ISerializationError err)
    {
      return Std.Wrappers.Outcome<Std.JSON.Errors._ISerializationError>.Need((new BigInteger((s).Count)) < (Std.BoundedInts.__default.TWO__TO__THE__32), err);
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._Ijstring, Std.JSON.Errors._ISerializationError> String(Dafny.ISequence<Dafny.Rune> str) {
      Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Spec.__default.EscapeToUTF8(str, BigInteger.Zero);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._Ijstring>();
      } else {
        Dafny.ISequence<byte> _1_bs = (_0_valueOrError0).Extract();
        Std.Wrappers._IOutcome<Std.JSON.Errors._ISerializationError> _2_o = Std.JSON.Serializer.__default.CheckLength<byte>(_1_bs, Std.JSON.Errors.SerializationError.create_StringTooLong(str));
        if ((_2_o).is_Pass) {
          return Std.Wrappers.Result<Std.JSON.Grammar._Ijstring, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.jstring.create(Std.JSON.Grammar.__default.DOUBLEQUOTE, Std.JSON.Utils.Views.Core.View__.OfBytes(_1_bs), Std.JSON.Grammar.__default.DOUBLEQUOTE));
        } else {
          return Std.Wrappers.Result<Std.JSON.Grammar._Ijstring, Std.JSON.Errors._ISerializationError>.create_Failure((_2_o).dtor_error);
        }
      }
    }
    public static Std.JSON.Utils.Views.Core._IView__ Sign(BigInteger n) {
      return Std.JSON.Utils.Views.Core.View__.OfBytes((((n).Sign == -1) ? (Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('-')).Value))) : (Dafny.Sequence<byte>.FromElements())));
    }
    public static Dafny.ISequence<byte> Int_k(BigInteger n) {
      return Std.JSON.ByteStrConversion.__default.OfInt(n, Std.JSON.Serializer.__default.MINUS);
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError> Int(BigInteger n) {
      Dafny.ISequence<byte> _0_bs = Std.JSON.Serializer.__default.Int_k(n);
      Std.Wrappers._IOutcome<Std.JSON.Errors._ISerializationError> _1_o = Std.JSON.Serializer.__default.CheckLength<byte>(_0_bs, Std.JSON.Errors.SerializationError.create_IntTooLarge(n));
      if ((_1_o).is_Pass) {
        return Std.Wrappers.Result<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Utils.Views.Core.View__.OfBytes(_0_bs));
      } else {
        return Std.Wrappers.Result<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError>.create_Failure((_1_o).dtor_error);
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._Ijnumber, Std.JSON.Errors._ISerializationError> Number(Std.JSON.Values._IDecimal dec) {
      var _pat_let_tv0 = dec;
      var _pat_let_tv1 = dec;
      Std.JSON.Utils.Views.Core._IView__ _0_minus = Std.JSON.Serializer.__default.Sign((dec).dtor_n);
      Std.Wrappers._IResult<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError> _1_valueOrError0 = Std.JSON.Serializer.__default.Int(Std.Math.__default.Abs((dec).dtor_n));
      if ((_1_valueOrError0).IsFailure()) {
        return (_1_valueOrError0).PropagateFailure<Std.JSON.Grammar._Ijnumber>();
      } else {
        Std.JSON.Utils.Views.Core._IView__ _2_num = (_1_valueOrError0).Extract();
        Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> _3_frac = Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijfrac>.create_Empty();
        Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError> _4_valueOrError1 = ((((dec).dtor_e10).Sign == 0) ? (Std.Wrappers.Result<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijexp>.create_Empty())) : (Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements((byte)((new Dafny.Rune('e')).Value))), _pat_let1_0 => Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(_pat_let1_0, _5_e => Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(Std.JSON.Serializer.__default.Sign((_pat_let_tv0).dtor_e10), _pat_let2_0 => Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(_pat_let2_0, _6_sign => Dafny.Helpers.Let<Std.Wrappers._IResult<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError>, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(Std.JSON.Serializer.__default.Int(Std.Math.__default.Abs((_pat_let_tv1).dtor_e10)), _pat_let3_0 => Dafny.Helpers.Let<Std.Wrappers._IResult<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Errors._ISerializationError>, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(_pat_let3_0, _7_valueOrError2 => (((_7_valueOrError2).IsFailure()) ? ((_7_valueOrError2).PropagateFailure<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>()) : (Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>((_7_valueOrError2).Extract(), _pat_let4_0 => Dafny.Helpers.Let<Std.JSON.Utils.Views.Core._IView__, Std.Wrappers._IResult<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>>(_pat_let4_0, _8_num => Std.Wrappers.Result<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijexp>.create_NonEmpty(Std.JSON.Grammar.jexp.create(_5_e, _6_sign, _8_num)))))))))))))));
        if ((_4_valueOrError1).IsFailure()) {
          return (_4_valueOrError1).PropagateFailure<Std.JSON.Grammar._Ijnumber>();
        } else {
          Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> _9_exp = (_4_valueOrError1).Extract();
          return Std.Wrappers.Result<Std.JSON.Grammar._Ijnumber, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.jnumber.create(_0_minus, _2_num, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijfrac>.create_Empty(), _9_exp));
        }
      }
    }
    public static Std.JSON.Grammar._IStructural<__T> MkStructural<__T>(__T v) {
      return Std.JSON.Grammar.Structural<__T>.create(Std.JSON.Grammar.__default.EMPTY, v, Std.JSON.Grammar.__default.EMPTY);
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IjKeyValue, Std.JSON.Errors._ISerializationError> KeyValue(_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON> kv) {
      Std.Wrappers._IResult<Std.JSON.Grammar._Ijstring, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Serializer.__default.String((kv).dtor__0);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IjKeyValue>();
      } else {
        Std.JSON.Grammar._Ijstring _1_k = (_0_valueOrError0).Extract();
        Std.Wrappers._IResult<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError> _2_valueOrError1 = Std.JSON.Serializer.__default.Value((kv).dtor__1);
        if ((_2_valueOrError1).IsFailure()) {
          return (_2_valueOrError1).PropagateFailure<Std.JSON.Grammar._IjKeyValue>();
        } else {
          Std.JSON.Grammar._IValue _3_v = (_2_valueOrError1).Extract();
          return Std.Wrappers.Result<Std.JSON.Grammar._IjKeyValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.jKeyValue.create(_1_k, Std.JSON.Serializer.__default.COLON, _3_v));
        }
      }
    }
    public static Dafny.ISequence<Std.JSON.Grammar._ISuffixed<__D, __S>> MkSuffixedSequence<__D, __S>(Dafny.ISequence<__D> ds, Std.JSON.Grammar._IStructural<__S> suffix, BigInteger start)
    {
      Dafny.ISequence<Std.JSON.Grammar._ISuffixed<__D, __S>> _0___accumulator = Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.FromElements();
    TAIL_CALL_START: ;
      if ((start) >= (new BigInteger((ds).Count))) {
        return Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.Concat(_0___accumulator, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.FromElements());
      } else if ((start) == ((new BigInteger((ds).Count)) - (BigInteger.One))) {
        return Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.Concat(_0___accumulator, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.FromElements(Std.JSON.Grammar.Suffixed<__D, __S>.create((ds).Select(start), Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<__S>>.create_Empty())));
      } else {
        _0___accumulator = Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.Concat(_0___accumulator, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<__D, __S>>.FromElements(Std.JSON.Grammar.Suffixed<__D, __S>.create((ds).Select(start), Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<__S>>.create_NonEmpty(suffix))));
        Dafny.ISequence<__D> _in0 = ds;
        Std.JSON.Grammar._IStructural<__S> _in1 = suffix;
        BigInteger _in2 = (start) + (BigInteger.One);
        ds = _in0;
        suffix = _in1;
        start = _in2;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError> Object(Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> obj) {
      Std.Wrappers._IResult<Dafny.ISequence<Std.JSON.Grammar._IjKeyValue>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.Collections.Seq.__default.MapWithResult<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Grammar._IjKeyValue, Std.JSON.Errors._ISerializationError>(Dafny.Helpers.Id<Func<Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>>, Func<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.Wrappers._IResult<Std.JSON.Grammar._IjKeyValue, Std.JSON.Errors._ISerializationError>>>>((_1_obj) => ((System.Func<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.Wrappers._IResult<Std.JSON.Grammar._IjKeyValue, Std.JSON.Errors._ISerializationError>>)((_2_v) => {
        return Std.JSON.Serializer.__default.KeyValue(_2_v);
      })))(obj), obj);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Dafny.ISequence<Std.JSON.Grammar._IjKeyValue> _3_items = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Bracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>.create(Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.LBRACE), Std.JSON.Serializer.__default.MkSuffixedSequence<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>(_3_items, Std.JSON.Serializer.__default.COMMA, BigInteger.Zero), Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.RBRACE)));
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError> Array(Dafny.ISequence<Std.JSON.Values._IJSON> arr) {
      Std.Wrappers._IResult<Dafny.ISequence<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.Collections.Seq.__default.MapWithResult<Std.JSON.Values._IJSON, Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>(Dafny.Helpers.Id<Func<Dafny.ISequence<Std.JSON.Values._IJSON>, Func<Std.JSON.Values._IJSON, Std.Wrappers._IResult<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>>>>((_1_arr) => ((System.Func<Std.JSON.Values._IJSON, Std.Wrappers._IResult<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>>)((_2_v) => {
        return Std.JSON.Serializer.__default.Value(_2_v);
      })))(arr), arr);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Dafny.ISequence<Std.JSON.Grammar._IValue> _3_items = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Bracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>.create(Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.LBRACKET), Std.JSON.Serializer.__default.MkSuffixedSequence<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>(_3_items, Std.JSON.Serializer.__default.COMMA, BigInteger.Zero), Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.RBRACKET)));
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError> Value(Std.JSON.Values._IJSON js) {
      Std.JSON.Values._IJSON _source0 = js;
      {
        if (_source0.is_Null) {
          return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_Null(Std.JSON.Utils.Views.Core.View__.OfBytes(Std.JSON.Grammar.__default.NULL)));
        }
      }
      {
        if (_source0.is_Bool) {
          bool _0_b = _source0.dtor_b;
          return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_Bool(Std.JSON.Serializer.__default.Bool(_0_b)));
        }
      }
      {
        if (_source0.is_String) {
          Dafny.ISequence<Dafny.Rune> _1_str = _source0.dtor_str;
          Std.Wrappers._IResult<Std.JSON.Grammar._Ijstring, Std.JSON.Errors._ISerializationError> _2_valueOrError0 = Std.JSON.Serializer.__default.String(_1_str);
          if ((_2_valueOrError0).IsFailure()) {
            return (_2_valueOrError0).PropagateFailure<Std.JSON.Grammar._IValue>();
          } else {
            Std.JSON.Grammar._Ijstring _3_s = (_2_valueOrError0).Extract();
            return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_String(_3_s));
          }
        }
      }
      {
        if (_source0.is_Number) {
          Std.JSON.Values._IDecimal _4_dec = _source0.dtor_num;
          Std.Wrappers._IResult<Std.JSON.Grammar._Ijnumber, Std.JSON.Errors._ISerializationError> _5_valueOrError1 = Std.JSON.Serializer.__default.Number(_4_dec);
          if ((_5_valueOrError1).IsFailure()) {
            return (_5_valueOrError1).PropagateFailure<Std.JSON.Grammar._IValue>();
          } else {
            Std.JSON.Grammar._Ijnumber _6_n = (_5_valueOrError1).Extract();
            return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_Number(_6_n));
          }
        }
      }
      {
        if (_source0.is_Object) {
          Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> _7_obj = _source0.dtor_obj;
          Std.Wrappers._IResult<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError> _8_valueOrError2 = Std.JSON.Serializer.__default.Object(_7_obj);
          if ((_8_valueOrError2).IsFailure()) {
            return (_8_valueOrError2).PropagateFailure<Std.JSON.Grammar._IValue>();
          } else {
            Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _9_o = (_8_valueOrError2).Extract();
            return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_Object(_9_o));
          }
        }
      }
      {
        Dafny.ISequence<Std.JSON.Values._IJSON> _10_arr = _source0.dtor_arr;
        Std.Wrappers._IResult<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Errors._ISerializationError> _11_valueOrError3 = Std.JSON.Serializer.__default.Array(_10_arr);
        if ((_11_valueOrError3).IsFailure()) {
          return (_11_valueOrError3).PropagateFailure<Std.JSON.Grammar._IValue>();
        } else {
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _12_a = (_11_valueOrError3).Extract();
          return Std.Wrappers.Result<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Grammar.Value.create_Array(_12_a));
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError> JSON(Std.JSON.Values._IJSON js) {
      Std.Wrappers._IResult<Std.JSON.Grammar._IValue, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Serializer.__default.Value(js);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>();
      } else {
        Std.JSON.Grammar._IValue _1_val = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError>.create_Success(Std.JSON.Serializer.__default.MkStructural<Std.JSON.Grammar._IValue>(_1_val));
      }
    }
    public static Dafny.ISequence<byte> DIGITS { get {
      return Std.JSON.ByteStrConversion.__default.chars;
    } }
    public static byte MINUS { get {
      return (byte)((new Dafny.Rune('-')).Value);
    } }
    public static Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> COLON { get {
      return Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.COLON);
    } }
    public static Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> COMMA { get {
      return Std.JSON.Serializer.__default.MkStructural<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.Grammar.__default.COMMA);
    } }
  }

  public partial class bytes32 {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<byte>>(Dafny.Sequence<byte>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<byte>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<byte> __source) {
      Dafny.ISequence<byte> _0_bs = __source;
      return (new BigInteger((_0_bs).Count)) < (Std.BoundedInts.__default.TWO__TO__THE__32);
    }
  }

  public partial class string32 {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>>(Dafny.Sequence<Dafny.Rune>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<Dafny.Rune>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<Dafny.Rune> __source) {
      Dafny.ISequence<Dafny.Rune> _1_s = __source;
      return (new BigInteger((_1_s).Count)) < (Std.BoundedInts.__default.TWO__TO__THE__32);
    }
  }
} // end of namespace Std.JSON.Serializer
namespace Std.JSON.Deserializer.Uint16StrConversion {

  public partial class __default {
    public static BigInteger BASE() {
      return Std.JSON.Deserializer.Uint16StrConversion.__default.@base;
    }
    public static bool IsDigitChar(ushort c) {
      return (Std.JSON.Deserializer.Uint16StrConversion.__default.charToDigit).Contains(c);
    }
    public static Dafny.ISequence<ushort> OfDigits(Dafny.ISequence<BigInteger> digits) {
      Dafny.ISequence<ushort> _0___accumulator = Dafny.Sequence<ushort>.FromElements();
    TAIL_CALL_START: ;
      if ((digits).Equals(Dafny.Sequence<BigInteger>.FromElements())) {
        return Dafny.Sequence<ushort>.Concat(Dafny.Sequence<ushort>.FromElements(), _0___accumulator);
      } else {
        _0___accumulator = Dafny.Sequence<ushort>.Concat(Dafny.Sequence<ushort>.FromElements((Std.JSON.Deserializer.Uint16StrConversion.__default.chars).Select((digits).Select(BigInteger.Zero))), _0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = (digits).Drop(BigInteger.One);
        digits = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<ushort> OfNat(BigInteger n) {
      if ((n).Sign == 0) {
        return Dafny.Sequence<ushort>.FromElements((Std.JSON.Deserializer.Uint16StrConversion.__default.chars).Select(BigInteger.Zero));
      } else {
        return Std.JSON.Deserializer.Uint16StrConversion.__default.OfDigits(Std.JSON.Deserializer.Uint16StrConversion.__default.FromNat(n));
      }
    }
    public static bool IsNumberStr(Dafny.ISequence<ushort> str, ushort minus)
    {
      return !(!(str).Equals(Dafny.Sequence<ushort>.FromElements())) || (((((str).Select(BigInteger.Zero)) == (minus)) || ((Std.JSON.Deserializer.Uint16StrConversion.__default.charToDigit).Contains((str).Select(BigInteger.Zero)))) && (Dafny.Helpers.Id<Func<Dafny.ISequence<ushort>, bool>>((_0_str) => Dafny.Helpers.Quantifier<ushort>(((_0_str).Drop(BigInteger.One)).UniqueElements, true, (((_forall_var_0) => {
        ushort _1_c = (ushort)_forall_var_0;
        return !(((_0_str).Drop(BigInteger.One)).Contains(_1_c)) || (Std.JSON.Deserializer.Uint16StrConversion.__default.IsDigitChar(_1_c));
      }))))(str)));
    }
    public static Dafny.ISequence<ushort> OfInt(BigInteger n, ushort minus)
    {
      if ((n).Sign != -1) {
        return Std.JSON.Deserializer.Uint16StrConversion.__default.OfNat(n);
      } else {
        return Dafny.Sequence<ushort>.Concat(Dafny.Sequence<ushort>.FromElements(minus), Std.JSON.Deserializer.Uint16StrConversion.__default.OfNat((BigInteger.Zero) - (n)));
      }
    }
    public static BigInteger ToNat(Dafny.ISequence<ushort> str) {
      if ((str).Equals(Dafny.Sequence<ushort>.FromElements())) {
        return BigInteger.Zero;
      } else {
        ushort _0_c = (str).Select((new BigInteger((str).Count)) - (BigInteger.One));
        return ((Std.JSON.Deserializer.Uint16StrConversion.__default.ToNat((str).Take((new BigInteger((str).Count)) - (BigInteger.One)))) * (Std.JSON.Deserializer.Uint16StrConversion.__default.@base)) + (Dafny.Map<ushort, BigInteger>.Select(Std.JSON.Deserializer.Uint16StrConversion.__default.charToDigit,_0_c));
      }
    }
    public static BigInteger ToInt(Dafny.ISequence<ushort> str, ushort minus)
    {
      if (Dafny.Sequence<ushort>.IsPrefixOf(Dafny.Sequence<ushort>.FromElements(minus), str)) {
        return (BigInteger.Zero) - (Std.JSON.Deserializer.Uint16StrConversion.__default.ToNat((str).Drop(BigInteger.One)));
      } else {
        return Std.JSON.Deserializer.Uint16StrConversion.__default.ToNat(str);
      }
    }
    public static BigInteger ToNatRight(Dafny.ISequence<BigInteger> xs) {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return BigInteger.Zero;
      } else {
        return ((Std.JSON.Deserializer.Uint16StrConversion.__default.ToNatRight(Std.Collections.Seq.__default.DropFirst<BigInteger>(xs))) * (Std.JSON.Deserializer.Uint16StrConversion.__default.BASE())) + (Std.Collections.Seq.__default.First<BigInteger>(xs));
      }
    }
    public static BigInteger ToNatLeft(Dafny.ISequence<BigInteger> xs) {
      BigInteger _0___accumulator = BigInteger.Zero;
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return (BigInteger.Zero) + (_0___accumulator);
      } else {
        _0___accumulator = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) * (Std.Arithmetic.Power.__default.Pow(Std.JSON.Deserializer.Uint16StrConversion.__default.BASE(), (new BigInteger((xs).Count)) - (BigInteger.One)))) + (_0___accumulator);
        Dafny.ISequence<BigInteger> _in0 = Std.Collections.Seq.__default.DropLast<BigInteger>(xs);
        xs = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> FromNat(BigInteger n) {
      Dafny.ISequence<BigInteger> _0___accumulator = Dafny.Sequence<BigInteger>.FromElements();
    TAIL_CALL_START: ;
      if ((n).Sign == 0) {
        return Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<BigInteger>.Concat(_0___accumulator, Dafny.Sequence<BigInteger>.FromElements(Dafny.Helpers.EuclideanModulus(n, Std.JSON.Deserializer.Uint16StrConversion.__default.BASE())));
        BigInteger _in0 = Dafny.Helpers.EuclideanDivision(n, Std.JSON.Deserializer.Uint16StrConversion.__default.BASE());
        n = _in0;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtend(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
    TAIL_CALL_START: ;
      if ((new BigInteger((xs).Count)) >= (n)) {
        return xs;
      } else {
        Dafny.ISequence<BigInteger> _in0 = Dafny.Sequence<BigInteger>.Concat(xs, Dafny.Sequence<BigInteger>.FromElements(BigInteger.Zero));
        BigInteger _in1 = n;
        xs = _in0;
        n = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<BigInteger> SeqExtendMultiple(Dafny.ISequence<BigInteger> xs, BigInteger n)
    {
      BigInteger _0_newLen = ((new BigInteger((xs).Count)) + (n)) - (Dafny.Helpers.EuclideanModulus(new BigInteger((xs).Count), n));
      return Std.JSON.Deserializer.Uint16StrConversion.__default.SeqExtend(xs, _0_newLen);
    }
    public static Dafny.ISequence<BigInteger> FromNatWithLen(BigInteger n, BigInteger len)
    {
      return Std.JSON.Deserializer.Uint16StrConversion.__default.SeqExtend(Std.JSON.Deserializer.Uint16StrConversion.__default.FromNat(n), len);
    }
    public static Dafny.ISequence<BigInteger> SeqZero(BigInteger len) {
      Dafny.ISequence<BigInteger> _0_xs = Std.JSON.Deserializer.Uint16StrConversion.__default.FromNatWithLen(BigInteger.Zero, len);
      return _0_xs;
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqAdd(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.JSON.Deserializer.Uint16StrConversion.__default.SeqAdd(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs_k = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        BigInteger _2_sum = ((Std.Collections.Seq.__default.Last<BigInteger>(xs)) + (Std.Collections.Seq.__default.Last<BigInteger>(ys))) + (_1_cin);
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((_2_sum) < (Std.JSON.Deserializer.Uint16StrConversion.__default.BASE())) ? (_System.Tuple2<BigInteger, BigInteger>.create(_2_sum, BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((_2_sum) - (Std.JSON.Deserializer.Uint16StrConversion.__default.BASE()), BigInteger.One)));
        BigInteger _3_sum__out = _let_tmp_rhs1.dtor__0;
        BigInteger _4_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs_k, Dafny.Sequence<BigInteger>.FromElements(_3_sum__out)), _4_cout);
      }
    }
    public static _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> SeqSub(Dafny.ISequence<BigInteger> xs, Dafny.ISequence<BigInteger> ys)
    {
      if ((new BigInteger((xs).Count)).Sign == 0) {
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.FromElements(), BigInteger.Zero);
      } else {
        _System._ITuple2<Dafny.ISequence<BigInteger>, BigInteger> _let_tmp_rhs0 = Std.JSON.Deserializer.Uint16StrConversion.__default.SeqSub(Std.Collections.Seq.__default.DropLast<BigInteger>(xs), Std.Collections.Seq.__default.DropLast<BigInteger>(ys));
        Dafny.ISequence<BigInteger> _0_zs = _let_tmp_rhs0.dtor__0;
        BigInteger _1_cin = _let_tmp_rhs0.dtor__1;
        _System._ITuple2<BigInteger, BigInteger> _let_tmp_rhs1 = (((Std.Collections.Seq.__default.Last<BigInteger>(xs)) >= ((Std.Collections.Seq.__default.Last<BigInteger>(ys)) + (_1_cin))) ? (_System.Tuple2<BigInteger, BigInteger>.create(((Std.Collections.Seq.__default.Last<BigInteger>(xs)) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.Zero)) : (_System.Tuple2<BigInteger, BigInteger>.create((((Std.JSON.Deserializer.Uint16StrConversion.__default.BASE()) + (Std.Collections.Seq.__default.Last<BigInteger>(xs))) - (Std.Collections.Seq.__default.Last<BigInteger>(ys))) - (_1_cin), BigInteger.One)));
        BigInteger _2_diff__out = _let_tmp_rhs1.dtor__0;
        BigInteger _3_cout = _let_tmp_rhs1.dtor__1;
        return _System.Tuple2<Dafny.ISequence<BigInteger>, BigInteger>.create(Dafny.Sequence<BigInteger>.Concat(_0_zs, Dafny.Sequence<BigInteger>.FromElements(_2_diff__out)), _3_cout);
      }
    }
    public static Dafny.ISequence<ushort> chars { get {
      return Dafny.Sequence<ushort>.FromElements((ushort)((new Dafny.Rune('0')).Value), (ushort)((new Dafny.Rune('1')).Value), (ushort)((new Dafny.Rune('2')).Value), (ushort)((new Dafny.Rune('3')).Value), (ushort)((new Dafny.Rune('4')).Value), (ushort)((new Dafny.Rune('5')).Value), (ushort)((new Dafny.Rune('6')).Value), (ushort)((new Dafny.Rune('7')).Value), (ushort)((new Dafny.Rune('8')).Value), (ushort)((new Dafny.Rune('9')).Value), (ushort)((new Dafny.Rune('a')).Value), (ushort)((new Dafny.Rune('b')).Value), (ushort)((new Dafny.Rune('c')).Value), (ushort)((new Dafny.Rune('d')).Value), (ushort)((new Dafny.Rune('e')).Value), (ushort)((new Dafny.Rune('f')).Value), (ushort)((new Dafny.Rune('A')).Value), (ushort)((new Dafny.Rune('B')).Value), (ushort)((new Dafny.Rune('C')).Value), (ushort)((new Dafny.Rune('D')).Value), (ushort)((new Dafny.Rune('E')).Value), (ushort)((new Dafny.Rune('F')).Value));
    } }
    public static BigInteger @base { get {
      return new BigInteger((Std.JSON.Deserializer.Uint16StrConversion.__default.chars).Count);
    } }
    public static Dafny.IMap<ushort,BigInteger> charToDigit { get {
      return Dafny.Map<ushort, BigInteger>.FromElements(new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('0')).Value), BigInteger.Zero), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('1')).Value), BigInteger.One), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('2')).Value), new BigInteger(2)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('3')).Value), new BigInteger(3)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('4')).Value), new BigInteger(4)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('5')).Value), new BigInteger(5)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('6')).Value), new BigInteger(6)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('7')).Value), new BigInteger(7)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('8')).Value), new BigInteger(8)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('9')).Value), new BigInteger(9)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('a')).Value), new BigInteger(10)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('b')).Value), new BigInteger(11)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('c')).Value), new BigInteger(12)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('d')).Value), new BigInteger(13)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('e')).Value), new BigInteger(14)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('f')).Value), new BigInteger(15)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('A')).Value), new BigInteger(10)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('B')).Value), new BigInteger(11)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('C')).Value), new BigInteger(12)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('D')).Value), new BigInteger(13)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('E')).Value), new BigInteger(14)), new Dafny.Pair<ushort, BigInteger>((ushort)((new Dafny.Rune('F')).Value), new BigInteger(15)));
    } }
  }

  public partial class CharSeq {
    private static readonly Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TYPE = new Dafny.TypeDescriptor<Dafny.ISequence<ushort>>(Dafny.Sequence<ushort>.Empty);
    public static Dafny.TypeDescriptor<Dafny.ISequence<ushort>> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(Dafny.ISequence<ushort> __source) {
      Dafny.ISequence<ushort> _0_chars = __source;
      return (new BigInteger((_0_chars).Count)) > (BigInteger.One);
    }
  }

  public partial class digit {
    private static readonly Dafny.TypeDescriptor<BigInteger> _TYPE = new Dafny.TypeDescriptor<BigInteger>(BigInteger.Zero);
    public static Dafny.TypeDescriptor<BigInteger> _TypeDescriptor() {
      return _TYPE;
    }
    public static bool _Is(BigInteger __source) {
      BigInteger _1_i = __source;
      if (_System.nat._Is(_1_i)) {
        return ((_1_i).Sign != -1) && ((_1_i) < (Std.JSON.Deserializer.Uint16StrConversion.__default.BASE()));
      }
      return false;
    }
  }
} // end of namespace Std.JSON.Deserializer.Uint16StrConversion
namespace Std.JSON.Deserializer {

  public partial class __default {
    public static bool Bool(Std.JSON.Utils.Views.Core._IView__ js) {
      return ((js).At(0U)) == ((byte)((new Dafny.Rune('t')).Value));
    }
    public static Std.JSON.Errors._IDeserializationError UnsupportedEscape16(Dafny.ISequence<ushort> code) {
      return Std.JSON.Errors.DeserializationError.create_UnsupportedEscape(Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.GetOr(Std.Unicode.UnicodeStringsWithUnicodeChar.__default.FromUTF16Checked(code), Dafny.Sequence<Dafny.Rune>.UnicodeFromString("Couldn't decode UTF-16")));
    }
    public static ushort ToNat16(Dafny.ISequence<ushort> str) {
      BigInteger _0_hd = Std.JSON.Deserializer.Uint16StrConversion.__default.ToNat(str);
      return (ushort)(_0_hd);
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError> Unescape(Dafny.ISequence<ushort> str, BigInteger start, Dafny.ISequence<ushort> prefix)
    {
    TAIL_CALL_START: ;
      if ((start) >= (new BigInteger((str).Count))) {
        return Std.Wrappers.Result<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError>.create_Success(prefix);
      } else if (((str).Select(start)) == ((ushort)((new Dafny.Rune('\\')).Value))) {
        if ((new BigInteger((str).Count)) == ((start) + (BigInteger.One))) {
          return Std.Wrappers.Result<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError>.create_Failure(Std.JSON.Errors.DeserializationError.create_EscapeAtEOS());
        } else {
          ushort _0_c = (str).Select((start) + (BigInteger.One));
          if ((_0_c) == ((ushort)((new Dafny.Rune('u')).Value))) {
            if ((new BigInteger((str).Count)) <= ((start) + (new BigInteger(6)))) {
              return Std.Wrappers.Result<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError>.create_Failure(Std.JSON.Errors.DeserializationError.create_EscapeAtEOS());
            } else {
              Dafny.ISequence<ushort> _1_code = (str).Subsequence((start) + (new BigInteger(2)), (start) + (new BigInteger(6)));
              if (Dafny.Helpers.Id<Func<Dafny.ISequence<ushort>, bool>>((_2_code) => Dafny.Helpers.Quantifier<ushort>((_2_code).UniqueElements, false, (((_exists_var_0) => {
                ushort _3_c = (ushort)_exists_var_0;
                return ((_2_code).Contains(_3_c)) && (!(Std.JSON.Deserializer.__default.HEX__TABLE__16).Contains(_3_c));
              }))))(_1_code)) {
                return Std.Wrappers.Result<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError>.create_Failure(Std.JSON.Deserializer.__default.UnsupportedEscape16(_1_code));
              } else {
                ushort _4_hd = Std.JSON.Deserializer.__default.ToNat16(_1_code);
                Dafny.ISequence<ushort> _in0 = str;
                BigInteger _in1 = (start) + (new BigInteger(6));
                Dafny.ISequence<ushort> _in2 = Dafny.Sequence<ushort>.Concat(prefix, Dafny.Sequence<ushort>.FromElements(_4_hd));
                str = _in0;
                start = _in1;
                prefix = _in2;
                goto TAIL_CALL_START;
              }
            }
          } else {
            ushort _5_unescaped = ((System.Func<ushort>)(() => {
              ushort _source0 = _0_c;
              {
                if ((_source0) == ((ushort)(34))) {
                  return (ushort)(34);
                }
              }
              {
                if ((_source0) == ((ushort)(92))) {
                  return (ushort)(92);
                }
              }
              {
                if ((_source0) == ((ushort)(98))) {
                  return (ushort)(8);
                }
              }
              {
                if ((_source0) == ((ushort)(102))) {
                  return (ushort)(12);
                }
              }
              {
                if ((_source0) == ((ushort)(110))) {
                  return (ushort)(10);
                }
              }
              {
                if ((_source0) == ((ushort)(114))) {
                  return (ushort)(13);
                }
              }
              {
                if ((_source0) == ((ushort)(116))) {
                  return (ushort)(9);
                }
              }
              {
                return (ushort)(0);
              }
            }))();
            if ((new BigInteger(_5_unescaped)).Sign == 0) {
              return Std.Wrappers.Result<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError>.create_Failure(Std.JSON.Deserializer.__default.UnsupportedEscape16((str).Subsequence(start, (start) + (new BigInteger(2)))));
            } else {
              Dafny.ISequence<ushort> _in3 = str;
              BigInteger _in4 = (start) + (new BigInteger(2));
              Dafny.ISequence<ushort> _in5 = Dafny.Sequence<ushort>.Concat(prefix, Dafny.Sequence<ushort>.FromElements(_5_unescaped));
              str = _in3;
              start = _in4;
              prefix = _in5;
              goto TAIL_CALL_START;
            }
          }
        }
      } else {
        Dafny.ISequence<ushort> _in6 = str;
        BigInteger _in7 = (start) + (BigInteger.One);
        Dafny.ISequence<ushort> _in8 = Dafny.Sequence<ushort>.Concat(prefix, Dafny.Sequence<ushort>.FromElements((str).Select(start)));
        str = _in6;
        start = _in7;
        prefix = _in8;
        goto TAIL_CALL_START;
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<Dafny.Rune>, Std.JSON.Errors._IDeserializationError> String(Std.JSON.Grammar._Ijstring js) {
      Std.Wrappers._IResult<Dafny.ISequence<Dafny.Rune>, Std.JSON.Errors._IDeserializationError> _0_valueOrError0 = (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.FromUTF8Checked(((js).dtor_contents).Bytes())).ToResult<Std.JSON.Errors._IDeserializationError>(Std.JSON.Errors.DeserializationError.create_InvalidUnicode());
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
      } else {
        Dafny.ISequence<Dafny.Rune> _1_asUtf32 = (_0_valueOrError0).Extract();
        Std.Wrappers._IResult<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError> _2_valueOrError1 = (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.ToUTF16Checked(_1_asUtf32)).ToResult<Std.JSON.Errors._IDeserializationError>(Std.JSON.Errors.DeserializationError.create_InvalidUnicode());
        if ((_2_valueOrError1).IsFailure()) {
          return (_2_valueOrError1).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
        } else {
          Dafny.ISequence<ushort> _3_asUint16 = (_2_valueOrError1).Extract();
          Std.Wrappers._IResult<Dafny.ISequence<ushort>, Std.JSON.Errors._IDeserializationError> _4_valueOrError2 = Std.JSON.Deserializer.__default.Unescape(_3_asUint16, BigInteger.Zero, Dafny.Sequence<ushort>.FromElements());
          if ((_4_valueOrError2).IsFailure()) {
            return (_4_valueOrError2).PropagateFailure<Dafny.ISequence<Dafny.Rune>>();
          } else {
            Dafny.ISequence<ushort> _5_unescaped = (_4_valueOrError2).Extract();
            return (Std.Unicode.UnicodeStringsWithUnicodeChar.__default.FromUTF16Checked(_5_unescaped)).ToResult<Std.JSON.Errors._IDeserializationError>(Std.JSON.Errors.DeserializationError.create_InvalidUnicode());
          }
        }
      }
    }
    public static Std.Wrappers._IResult<BigInteger, Std.JSON.Errors._IDeserializationError> ToInt(Std.JSON.Utils.Views.Core._IView__ sign, Std.JSON.Utils.Views.Core._IView__ n)
    {
      BigInteger _0_n = Std.JSON.ByteStrConversion.__default.ToNat((n).Bytes());
      return Std.Wrappers.Result<BigInteger, Std.JSON.Errors._IDeserializationError>.create_Success((((sign).Char_q(new Dafny.Rune('-'))) ? ((BigInteger.Zero) - (_0_n)) : (_0_n)));
    }
    public static Std.Wrappers._IResult<Std.JSON.Values._IDecimal, Std.JSON.Errors._IDeserializationError> Number(Std.JSON.Grammar._Ijnumber js) {
      Std.JSON.Grammar._Ijnumber _let_tmp_rhs0 = js;
      Std.JSON.Utils.Views.Core._IView__ _0_minus = _let_tmp_rhs0.dtor_minus;
      Std.JSON.Utils.Views.Core._IView__ _1_num = _let_tmp_rhs0.dtor_num;
      Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> _2_frac = _let_tmp_rhs0.dtor_frac;
      Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> _3_exp = _let_tmp_rhs0.dtor_exp;
      Std.Wrappers._IResult<BigInteger, Std.JSON.Errors._IDeserializationError> _4_valueOrError0 = Std.JSON.Deserializer.__default.ToInt(_0_minus, _1_num);
      if ((_4_valueOrError0).IsFailure()) {
        return (_4_valueOrError0).PropagateFailure<Std.JSON.Values._IDecimal>();
      } else {
        BigInteger _5_n = (_4_valueOrError0).Extract();
        Std.Wrappers._IResult<BigInteger, Std.JSON.Errors._IDeserializationError> _6_valueOrError1 = ((System.Func<Std.Wrappers._IResult<BigInteger, Std.JSON.Errors._IDeserializationError>>)(() => {
          Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp> _source0 = _3_exp;
          {
            if (_source0.is_Empty) {
              return Std.Wrappers.Result<BigInteger, Std.JSON.Errors._IDeserializationError>.create_Success(BigInteger.Zero);
            }
          }
          {
            Std.JSON.Grammar._Ijexp t0 = _source0.dtor_t;
            Std.JSON.Utils.Views.Core._IView__ _7_sign = t0.dtor_sign;
            Std.JSON.Utils.Views.Core._IView__ _8_num = t0.dtor_num;
            return Std.JSON.Deserializer.__default.ToInt(_7_sign, _8_num);
          }
        }))();
        if ((_6_valueOrError1).IsFailure()) {
          return (_6_valueOrError1).PropagateFailure<Std.JSON.Values._IDecimal>();
        } else {
          BigInteger _9_e10 = (_6_valueOrError1).Extract();
          Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac> _source1 = _2_frac;
          {
            if (_source1.is_Empty) {
              return Std.Wrappers.Result<Std.JSON.Values._IDecimal, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.Decimal.create(_5_n, _9_e10));
            }
          }
          {
            Std.JSON.Grammar._Ijfrac t1 = _source1.dtor_t;
            Std.JSON.Utils.Views.Core._IView__ _10_num = t1.dtor_num;
            BigInteger _11_pow10 = new BigInteger((_10_num).Length());
            Std.Wrappers._IResult<BigInteger, Std.JSON.Errors._IDeserializationError> _12_valueOrError2 = Std.JSON.Deserializer.__default.ToInt(_0_minus, _10_num);
            if ((_12_valueOrError2).IsFailure()) {
              return (_12_valueOrError2).PropagateFailure<Std.JSON.Values._IDecimal>();
            } else {
              BigInteger _13_frac = (_12_valueOrError2).Extract();
              return Std.Wrappers.Result<Std.JSON.Values._IDecimal, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.Decimal.create(((_5_n) * (Std.Arithmetic.Power.__default.Pow(new BigInteger(10), _11_pow10))) + (_13_frac), (_9_e10) - (_11_pow10)));
            }
          }
        }
      }
    }
    public static Std.Wrappers._IResult<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError> KeyValue(Std.JSON.Grammar._IjKeyValue js) {
      Std.Wrappers._IResult<Dafny.ISequence<Dafny.Rune>, Std.JSON.Errors._IDeserializationError> _0_valueOrError0 = Std.JSON.Deserializer.__default.String((js).dtor_k);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>>();
      } else {
        Dafny.ISequence<Dafny.Rune> _1_k = (_0_valueOrError0).Extract();
        Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError> _2_valueOrError1 = Std.JSON.Deserializer.__default.Value((js).dtor_v);
        if ((_2_valueOrError1).IsFailure()) {
          return (_2_valueOrError1).PropagateFailure<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>>();
        } else {
          Std.JSON.Values._IJSON _3_v = (_2_valueOrError1).Extract();
          return Std.Wrappers.Result<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError>.create_Success(_System.Tuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>.create(_1_k, _3_v));
        }
      }
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>>, Std.JSON.Errors._IDeserializationError> Object(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> js) {
      return Std.Collections.Seq.__default.MapWithResult<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>, _System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError>(Dafny.Helpers.Id<Func<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>, Std.Wrappers._IResult<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError>>>>((_0_js) => ((System.Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>, Std.Wrappers._IResult<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError>>)((_1_d) => {
        return Std.JSON.Deserializer.__default.KeyValue((_1_d).dtor_t);
      })))(js), (js).dtor_data);
    }
    public static Std.Wrappers._IResult<Dafny.ISequence<Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError> Array(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> js) {
      return Std.Collections.Seq.__default.MapWithResult<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>(Dafny.Helpers.Id<Func<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>, Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>>>>((_0_js) => ((System.Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>, Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>>)((_1_d) => {
        return Std.JSON.Deserializer.__default.Value((_1_d).dtor_t);
      })))(js), (js).dtor_data);
    }
    public static Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError> Value(Std.JSON.Grammar._IValue js) {
      Std.JSON.Grammar._IValue _source0 = js;
      {
        if (_source0.is_Null) {
          return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_Null());
        }
      }
      {
        if (_source0.is_Bool) {
          Std.JSON.Utils.Views.Core._IView__ _0_b = _source0.dtor_b;
          return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_Bool(Std.JSON.Deserializer.__default.Bool(_0_b)));
        }
      }
      {
        if (_source0.is_String) {
          Std.JSON.Grammar._Ijstring _1_str = _source0.dtor_str;
          Std.Wrappers._IResult<Dafny.ISequence<Dafny.Rune>, Std.JSON.Errors._IDeserializationError> _2_valueOrError0 = Std.JSON.Deserializer.__default.String(_1_str);
          if ((_2_valueOrError0).IsFailure()) {
            return (_2_valueOrError0).PropagateFailure<Std.JSON.Values._IJSON>();
          } else {
            Dafny.ISequence<Dafny.Rune> _3_s = (_2_valueOrError0).Extract();
            return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_String(_3_s));
          }
        }
      }
      {
        if (_source0.is_Number) {
          Std.JSON.Grammar._Ijnumber _4_dec = _source0.dtor_num;
          Std.Wrappers._IResult<Std.JSON.Values._IDecimal, Std.JSON.Errors._IDeserializationError> _5_valueOrError1 = Std.JSON.Deserializer.__default.Number(_4_dec);
          if ((_5_valueOrError1).IsFailure()) {
            return (_5_valueOrError1).PropagateFailure<Std.JSON.Values._IJSON>();
          } else {
            Std.JSON.Values._IDecimal _6_n = (_5_valueOrError1).Extract();
            return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_Number(_6_n));
          }
        }
      }
      {
        if (_source0.is_Object) {
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _7_obj = _source0.dtor_obj;
          Std.Wrappers._IResult<Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>>, Std.JSON.Errors._IDeserializationError> _8_valueOrError2 = Std.JSON.Deserializer.__default.Object(_7_obj);
          if ((_8_valueOrError2).IsFailure()) {
            return (_8_valueOrError2).PropagateFailure<Std.JSON.Values._IJSON>();
          } else {
            Dafny.ISequence<_System._ITuple2<Dafny.ISequence<Dafny.Rune>, Std.JSON.Values._IJSON>> _9_o = (_8_valueOrError2).Extract();
            return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_Object(_9_o));
          }
        }
      }
      {
        Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _10_arr = _source0.dtor_arr;
        Std.Wrappers._IResult<Dafny.ISequence<Std.JSON.Values._IJSON>, Std.JSON.Errors._IDeserializationError> _11_valueOrError3 = Std.JSON.Deserializer.__default.Array(_10_arr);
        if ((_11_valueOrError3).IsFailure()) {
          return (_11_valueOrError3).PropagateFailure<Std.JSON.Values._IJSON>();
        } else {
          Dafny.ISequence<Std.JSON.Values._IJSON> _12_a = (_11_valueOrError3).Extract();
          return Std.Wrappers.Result<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError>.create_Success(Std.JSON.Values.JSON.create_Array(_12_a));
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError> JSON(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js) {
      return Std.JSON.Deserializer.__default.Value((js).dtor_t);
    }
    public static Dafny.IMap<ushort,BigInteger> HEX__TABLE__16 { get {
      return Std.JSON.Deserializer.Uint16StrConversion.__default.charToDigit;
    } }
    public static Dafny.IMap<byte,BigInteger> DIGITS { get {
      return Std.JSON.ByteStrConversion.__default.charToDigit;
    } }
    public static byte MINUS { get {
      return (byte)((new Dafny.Rune('-')).Value);
    } }
  }
} // end of namespace Std.JSON.Deserializer
namespace Std.JSON.ConcreteSyntax.Spec {

  public partial class __default {
    public static Dafny.ISequence<byte> View(Std.JSON.Utils.Views.Core._IView__ v) {
      return (v).Bytes();
    }
    public static Dafny.ISequence<byte> Structural<__T>(Std.JSON.Grammar._IStructural<__T> self, Func<__T, Dafny.ISequence<byte>> fT)
    {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_before), Dafny.Helpers.Id<Func<__T, Dafny.ISequence<byte>>>(fT)((self).dtor_t)), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_after));
    }
    public static Dafny.ISequence<byte> StructuralView(Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> self) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Structural<Std.JSON.Utils.Views.Core._IView__>(self, Std.JSON.ConcreteSyntax.Spec.__default.View);
    }
    public static Dafny.ISequence<byte> Maybe<__T>(Std.JSON.Grammar._IMaybe<__T> self, Func<__T, Dafny.ISequence<byte>> fT)
    {
      if ((self).is_Empty) {
        return Dafny.Sequence<byte>.FromElements();
      } else {
        return Dafny.Helpers.Id<Func<__T, Dafny.ISequence<byte>>>(fT)((self).dtor_t);
      }
    }
    public static Dafny.ISequence<byte> ConcatBytes<__T>(Dafny.ISequence<__T> ts, Func<__T, Dafny.ISequence<byte>> fT)
    {
      Dafny.ISequence<byte> _0___accumulator = Dafny.Sequence<byte>.FromElements();
    TAIL_CALL_START: ;
      if ((new BigInteger((ts).Count)).Sign == 0) {
        return Dafny.Sequence<byte>.Concat(_0___accumulator, Dafny.Sequence<byte>.FromElements());
      } else {
        _0___accumulator = Dafny.Sequence<byte>.Concat(_0___accumulator, Dafny.Helpers.Id<Func<__T, Dafny.ISequence<byte>>>(fT)((ts).Select(BigInteger.Zero)));
        Dafny.ISequence<__T> _in0 = (ts).Drop(BigInteger.One);
        Func<__T, Dafny.ISequence<byte>> _in1 = fT;
        ts = _in0;
        fT = _in1;
        goto TAIL_CALL_START;
      }
    }
    public static Dafny.ISequence<byte> Bracketed<__D, __S>(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, __D, __S, Std.JSON.Utils.Views.Core._IView__> self, Func<Std.JSON.Grammar._ISuffixed<__D, __S>, Dafny.ISequence<byte>> fDatum)
    {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.StructuralView((self).dtor_l), Std.JSON.ConcreteSyntax.Spec.__default.ConcatBytes<Std.JSON.Grammar._ISuffixed<__D, __S>>((self).dtor_data, fDatum)), Std.JSON.ConcreteSyntax.Spec.__default.StructuralView((self).dtor_r));
    }
    public static Dafny.ISequence<byte> KeyValue(Std.JSON.Grammar._IjKeyValue self) {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.String((self).dtor_k), Std.JSON.ConcreteSyntax.Spec.__default.StructuralView((self).dtor_colon)), Std.JSON.ConcreteSyntax.Spec.__default.Value((self).dtor_v));
    }
    public static Dafny.ISequence<byte> Frac(Std.JSON.Grammar._Ijfrac self) {
      return Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_period), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_num));
    }
    public static Dafny.ISequence<byte> Exp(Std.JSON.Grammar._Ijexp self) {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_e), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_sign)), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_num));
    }
    public static Dafny.ISequence<byte> Number(Std.JSON.Grammar._Ijnumber self) {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_minus), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_num)), Std.JSON.ConcreteSyntax.Spec.__default.Maybe<Std.JSON.Grammar._Ijfrac>((self).dtor_frac, Std.JSON.ConcreteSyntax.Spec.__default.Frac)), Std.JSON.ConcreteSyntax.Spec.__default.Maybe<Std.JSON.Grammar._Ijexp>((self).dtor_exp, Std.JSON.ConcreteSyntax.Spec.__default.Exp));
    }
    public static Dafny.ISequence<byte> String(Std.JSON.Grammar._Ijstring self) {
      return Dafny.Sequence<byte>.Concat(Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_lq), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_contents)), Std.JSON.ConcreteSyntax.Spec.__default.View((self).dtor_rq));
    }
    public static Dafny.ISequence<byte> CommaSuffix(Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> c) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Maybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>(c, Std.JSON.ConcreteSyntax.Spec.__default.StructuralView);
    }
    public static Dafny.ISequence<byte> Member(Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__> self) {
      return Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.KeyValue((self).dtor_t), Std.JSON.ConcreteSyntax.Spec.__default.CommaSuffix((self).dtor_suffix));
    }
    public static Dafny.ISequence<byte> Item(Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__> self) {
      return Dafny.Sequence<byte>.Concat(Std.JSON.ConcreteSyntax.Spec.__default.Value((self).dtor_t), Std.JSON.ConcreteSyntax.Spec.__default.CommaSuffix((self).dtor_suffix));
    }
    public static Dafny.ISequence<byte> Object(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Bracketed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>(obj, Dafny.Helpers.Id<Func<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>, Dafny.ISequence<byte>>>>((_0_obj) => ((System.Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>, Dafny.ISequence<byte>>)((_1_d) => {
        return Std.JSON.ConcreteSyntax.Spec.__default.Member(_1_d);
      })))(obj));
    }
    public static Dafny.ISequence<byte> Array(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Bracketed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>(arr, Dafny.Helpers.Id<Func<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>, Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>, Dafny.ISequence<byte>>>>((_0_arr) => ((System.Func<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>, Dafny.ISequence<byte>>)((_1_d) => {
        return Std.JSON.ConcreteSyntax.Spec.__default.Item(_1_d);
      })))(arr));
    }
    public static Dafny.ISequence<byte> Value(Std.JSON.Grammar._IValue self) {
      Std.JSON.Grammar._IValue _source0 = self;
      {
        if (_source0.is_Null) {
          Std.JSON.Utils.Views.Core._IView__ _0_n = _source0.dtor_n;
          return Std.JSON.ConcreteSyntax.Spec.__default.View(_0_n);
        }
      }
      {
        if (_source0.is_Bool) {
          Std.JSON.Utils.Views.Core._IView__ _1_b = _source0.dtor_b;
          return Std.JSON.ConcreteSyntax.Spec.__default.View(_1_b);
        }
      }
      {
        if (_source0.is_String) {
          Std.JSON.Grammar._Ijstring _2_str = _source0.dtor_str;
          return Std.JSON.ConcreteSyntax.Spec.__default.String(_2_str);
        }
      }
      {
        if (_source0.is_Number) {
          Std.JSON.Grammar._Ijnumber _3_num = _source0.dtor_num;
          return Std.JSON.ConcreteSyntax.Spec.__default.Number(_3_num);
        }
      }
      {
        if (_source0.is_Object) {
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _4_obj = _source0.dtor_obj;
          return Std.JSON.ConcreteSyntax.Spec.__default.Object(_4_obj);
        }
      }
      {
        Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _5_arr = _source0.dtor_arr;
        return Std.JSON.ConcreteSyntax.Spec.__default.Array(_5_arr);
      }
    }
    public static Dafny.ISequence<byte> JSON(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Structural<Std.JSON.Grammar._IValue>(js, Std.JSON.ConcreteSyntax.Spec.__default.Value);
    }
  }
} // end of namespace Std.JSON.ConcreteSyntax.Spec
namespace Std.JSON.ConcreteSyntax.SpecProperties {

} // end of namespace Std.JSON.ConcreteSyntax.SpecProperties
namespace Std.JSON.ZeroCopy.Serializer {

  public partial class __default {
    public static Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> Serialize(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js)
    {
      Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> rbs = Std.Wrappers.Result<byte[], Std.JSON.Errors._ISerializationError>.Default(new byte[0]);
      Std.JSON.Utils.Views.Writers._IWriter__ _0_writer;
      _0_writer = Std.JSON.ZeroCopy.Serializer.__default.Text(js);
      Std.Wrappers._IOutcomeResult<Std.JSON.Errors._ISerializationError> _1_valueOrError0 = Std.Wrappers.OutcomeResult<Std.JSON.Errors._ISerializationError>.Default();
      _1_valueOrError0 = Std.Wrappers.__default.Need<Std.JSON.Errors._ISerializationError>((_0_writer).Unsaturated_q, Std.JSON.Errors.SerializationError.create_OutOfMemory());
      if ((_1_valueOrError0).IsFailure()) {
        rbs = (_1_valueOrError0).PropagateFailure<byte[]>();
        return rbs;
      }
      byte[] _2_bs;
      byte[] _out0;
      _out0 = (_0_writer).ToArray();
      _2_bs = _out0;
      rbs = Std.Wrappers.Result<byte[], Std.JSON.Errors._ISerializationError>.create_Success(_2_bs);
      return rbs;
      return rbs;
    }
    public static Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> SerializeTo(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js, byte[] dest)
    {
      Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> len = Std.Wrappers.Result<uint, Std.JSON.Errors._ISerializationError>.Default(0);
      Std.JSON.Utils.Views.Writers._IWriter__ _0_writer;
      _0_writer = Std.JSON.ZeroCopy.Serializer.__default.Text(js);
      Std.Wrappers._IOutcomeResult<Std.JSON.Errors._ISerializationError> _1_valueOrError0 = Std.Wrappers.OutcomeResult<Std.JSON.Errors._ISerializationError>.Default();
      _1_valueOrError0 = Std.Wrappers.__default.Need<Std.JSON.Errors._ISerializationError>((_0_writer).Unsaturated_q, Std.JSON.Errors.SerializationError.create_OutOfMemory());
      if ((_1_valueOrError0).IsFailure()) {
        len = (_1_valueOrError0).PropagateFailure<uint>();
        return len;
      }
      Std.Wrappers._IOutcomeResult<Std.JSON.Errors._ISerializationError> _2_valueOrError1 = Std.Wrappers.OutcomeResult<Std.JSON.Errors._ISerializationError>.Default();
      _2_valueOrError1 = Std.Wrappers.__default.Need<Std.JSON.Errors._ISerializationError>((new BigInteger((_0_writer).dtor_length)) <= (new BigInteger((dest).Length)), Std.JSON.Errors.SerializationError.create_OutOfMemory());
      if ((_2_valueOrError1).IsFailure()) {
        len = (_2_valueOrError1).PropagateFailure<uint>();
        return len;
      }
      (_0_writer).CopyTo(dest);
      len = Std.Wrappers.Result<uint, Std.JSON.Errors._ISerializationError>.create_Success((_0_writer).dtor_length);
      return len;
      return len;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Text(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js) {
      return Std.JSON.ZeroCopy.Serializer.__default.JSON(js, Std.JSON.Utils.Views.Writers.Writer__.Empty);
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ JSON(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      return (((writer).Append((js).dtor_before)).Then(Dafny.Helpers.Id<Func<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Func<Std.JSON.Utils.Views.Writers._IWriter__, Std.JSON.Utils.Views.Writers._IWriter__>>>((_0_js) => ((System.Func<Std.JSON.Utils.Views.Writers._IWriter__, Std.JSON.Utils.Views.Writers._IWriter__>)((_1_wr) => {
        return Std.JSON.ZeroCopy.Serializer.__default.Value((_0_js).dtor_t, _1_wr);
      })))(js))).Append((js).dtor_after);
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Value(Std.JSON.Grammar._IValue v, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Grammar._IValue _source0 = v;
      {
        if (_source0.is_Null) {
          Std.JSON.Utils.Views.Core._IView__ _0_n = _source0.dtor_n;
          Std.JSON.Utils.Views.Writers._IWriter__ _1_wr = (writer).Append(_0_n);
          return _1_wr;
        }
      }
      {
        if (_source0.is_Bool) {
          Std.JSON.Utils.Views.Core._IView__ _2_b = _source0.dtor_b;
          Std.JSON.Utils.Views.Writers._IWriter__ _3_wr = (writer).Append(_2_b);
          return _3_wr;
        }
      }
      {
        if (_source0.is_String) {
          Std.JSON.Grammar._Ijstring _4_str = _source0.dtor_str;
          Std.JSON.Utils.Views.Writers._IWriter__ _5_wr = Std.JSON.ZeroCopy.Serializer.__default.String(_4_str, writer);
          return _5_wr;
        }
      }
      {
        if (_source0.is_Number) {
          Std.JSON.Grammar._Ijnumber _6_num = _source0.dtor_num;
          Std.JSON.Utils.Views.Writers._IWriter__ _7_wr = Std.JSON.ZeroCopy.Serializer.__default.Number(_6_num, writer);
          return _7_wr;
        }
      }
      {
        if (_source0.is_Object) {
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _8_obj = _source0.dtor_obj;
          Std.JSON.Utils.Views.Writers._IWriter__ _9_wr = Std.JSON.ZeroCopy.Serializer.__default.Object(_8_obj, writer);
          return _9_wr;
        }
      }
      {
        Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _10_arr = _source0.dtor_arr;
        Std.JSON.Utils.Views.Writers._IWriter__ _11_wr = Std.JSON.ZeroCopy.Serializer.__default.Array(_10_arr, writer);
        return _11_wr;
      }
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ String(Std.JSON.Grammar._Ijstring str, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      return (((writer).Append((str).dtor_lq)).Append((str).dtor_contents)).Append((str).dtor_rq);
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Number(Std.JSON.Grammar._Ijnumber num, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ _0_wr1 = ((writer).Append((num).dtor_minus)).Append((num).dtor_num);
      Std.JSON.Utils.Views.Writers._IWriter__ _1_wr2 = ((((num).dtor_frac).is_NonEmpty) ? (((_0_wr1).Append((((num).dtor_frac).dtor_t).dtor_period)).Append((((num).dtor_frac).dtor_t).dtor_num)) : (_0_wr1));
      Std.JSON.Utils.Views.Writers._IWriter__ _2_wr3 = ((((num).dtor_exp).is_NonEmpty) ? ((((_1_wr2).Append((((num).dtor_exp).dtor_t).dtor_e)).Append((((num).dtor_exp).dtor_t).dtor_sign)).Append((((num).dtor_exp).dtor_t).dtor_num)) : (_1_wr2));
      Std.JSON.Utils.Views.Writers._IWriter__ _3_wr = _2_wr3;
      return _3_wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ StructuralView(Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__> st, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      return (((writer).Append((st).dtor_before)).Append((st).dtor_t)).Append((st).dtor_after);
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Object(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ _0_wr = Std.JSON.ZeroCopy.Serializer.__default.StructuralView((obj).dtor_l, writer);
      Std.JSON.Utils.Views.Writers._IWriter__ _1_wr = Std.JSON.ZeroCopy.Serializer.__default.Members(obj, _0_wr);
      Std.JSON.Utils.Views.Writers._IWriter__ _2_wr = Std.JSON.ZeroCopy.Serializer.__default.StructuralView((obj).dtor_r, _1_wr);
      return _2_wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Array(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ _0_wr = Std.JSON.ZeroCopy.Serializer.__default.StructuralView((arr).dtor_l, writer);
      Std.JSON.Utils.Views.Writers._IWriter__ _1_wr = Std.JSON.ZeroCopy.Serializer.__default.Items(arr, _0_wr);
      Std.JSON.Utils.Views.Writers._IWriter__ _2_wr = Std.JSON.ZeroCopy.Serializer.__default.StructuralView((arr).dtor_r, _1_wr);
      return _2_wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Members(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ wr = Std.JSON.Utils.Views.Writers.Writer.Default();
      Std.JSON.Utils.Views.Writers._IWriter__ _out0;
      _out0 = Std.JSON.ZeroCopy.Serializer.__default.MembersImpl(obj, writer);
      wr = _out0;
      return wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Items(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ wr = Std.JSON.Utils.Views.Writers.Writer.Default();
      Std.JSON.Utils.Views.Writers._IWriter__ _out0;
      _out0 = Std.JSON.ZeroCopy.Serializer.__default.ItemsImpl(arr, writer);
      wr = _out0;
      return wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ MembersImpl(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> obj, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ wr = Std.JSON.Utils.Views.Writers.Writer.Default();
      wr = writer;
      Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>> _0_members;
      _0_members = (obj).dtor_data;
      BigInteger _hi0 = new BigInteger((_0_members).Count);
      for (BigInteger _1_i = BigInteger.Zero; _1_i < _hi0; _1_i++) {
        wr = Std.JSON.ZeroCopy.Serializer.__default.Member((_0_members).Select(_1_i), wr);
      }
      return wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ ItemsImpl(Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> arr, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ wr = Std.JSON.Utils.Views.Writers.Writer.Default();
      wr = writer;
      Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>> _0_items;
      _0_items = (arr).dtor_data;
      BigInteger _hi0 = new BigInteger((_0_items).Count);
      for (BigInteger _1_i = BigInteger.Zero; _1_i < _hi0; _1_i++) {
        wr = Std.JSON.ZeroCopy.Serializer.__default.Item((_0_items).Select(_1_i), wr);
      }
      return wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Member(Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__> m, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ _0_wr = Std.JSON.ZeroCopy.Serializer.__default.String(((m).dtor_t).dtor_k, writer);
      Std.JSON.Utils.Views.Writers._IWriter__ _1_wr = Std.JSON.ZeroCopy.Serializer.__default.StructuralView(((m).dtor_t).dtor_colon, _0_wr);
      Std.JSON.Utils.Views.Writers._IWriter__ _2_wr = Std.JSON.ZeroCopy.Serializer.__default.Value(((m).dtor_t).dtor_v, _1_wr);
      Std.JSON.Utils.Views.Writers._IWriter__ _3_wr = ((((m).dtor_suffix).is_Empty) ? (_2_wr) : (Std.JSON.ZeroCopy.Serializer.__default.StructuralView(((m).dtor_suffix).dtor_t, _2_wr)));
      return _3_wr;
    }
    public static Std.JSON.Utils.Views.Writers._IWriter__ Item(Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__> m, Std.JSON.Utils.Views.Writers._IWriter__ writer)
    {
      Std.JSON.Utils.Views.Writers._IWriter__ _0_wr = Std.JSON.ZeroCopy.Serializer.__default.Value((m).dtor_t, writer);
      Std.JSON.Utils.Views.Writers._IWriter__ _1_wr = ((((m).dtor_suffix).is_Empty) ? (_0_wr) : (Std.JSON.ZeroCopy.Serializer.__default.StructuralView(((m).dtor_suffix).dtor_t, _0_wr)));
      return _1_wr;
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Serializer
namespace Std.JSON.ZeroCopy.Deserializer.Core {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Get(Std.JSON.Utils.Cursors._ICursor__ cs, Std.JSON.Errors._IDeserializationError err)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).Get<Std.JSON.Errors._IDeserializationError>(err);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> WS(Std.JSON.Utils.Cursors._ICursor__ cs)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Utils.Views.Core._IView__>.Default(Std.JSON.Grammar.jblanks.Default());
      uint _0_point_k;
      _0_point_k = (cs).dtor_point;
      uint _1_end;
      _1_end = (cs).dtor_end;
      while (((_0_point_k) < (_1_end)) && (Std.JSON.Grammar.__default.Blank_q(((cs).dtor_s).Select(_0_point_k)))) {
        _0_point_k = (_0_point_k) + (1U);
      }
      sp = (Std.JSON.Utils.Cursors.Cursor__.create((cs).dtor_s, (cs).dtor_beg, _0_point_k, (cs).dtor_end)).Split();
      return sp;
      return sp;
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<__T>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Structural<__T>(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> parser)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs0 = Std.JSON.ZeroCopy.Deserializer.Core.__default.WS(cs);
      Std.JSON.Utils.Views.Core._IView__ _0_before = _let_tmp_rhs0.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _1_cs = _let_tmp_rhs0.dtor_cs;
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _2_valueOrError0 = Dafny.Helpers.Id<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<__T>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>>((parser))(_1_cs);
      if ((_2_valueOrError0).IsFailure()) {
        return (_2_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<__T>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<__T> _let_tmp_rhs1 = (_2_valueOrError0).Extract();
        __T _3_val = _let_tmp_rhs1.dtor_t;
        Std.JSON.Utils.Cursors._ICursor__ _4_cs = _let_tmp_rhs1.dtor_cs;
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs2 = Std.JSON.ZeroCopy.Deserializer.Core.__default.WS(_4_cs);
        Std.JSON.Utils.Views.Core._IView__ _5_after = _let_tmp_rhs2.dtor_t;
        Std.JSON.Utils.Cursors._ICursor__ _6_cs = _let_tmp_rhs2.dtor_cs;
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<__T>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IStructural<__T>>.create(Std.JSON.Grammar.Structural<__T>.create(_0_before, _3_val, _5_after), _6_cs));
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> TryStructural(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs0 = Std.JSON.ZeroCopy.Deserializer.Core.__default.WS(cs);
      Std.JSON.Utils.Views.Core._IView__ _0_before = _let_tmp_rhs0.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _1_cs = _let_tmp_rhs0.dtor_cs;
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs1 = ((_1_cs).SkipByte()).Split();
      Std.JSON.Utils.Views.Core._IView__ _2_val = _let_tmp_rhs1.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _3_cs = _let_tmp_rhs1.dtor_cs;
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs2 = Std.JSON.ZeroCopy.Deserializer.Core.__default.WS(_3_cs);
      Std.JSON.Utils.Views.Core._IView__ _4_after = _let_tmp_rhs2.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _5_cs = _let_tmp_rhs2.dtor_cs;
      return Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>.create(Std.JSON.Grammar.Structural<Std.JSON.Utils.Views.Core._IView__>.create(_0_before, _2_val, _4_after), _5_cs);
    }
    public static Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>> SpecView { get {
      return ((System.Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>>)((_0_v) => {
        return Std.JSON.ConcreteSyntax.Spec.__default.View(_0_v);
      }));
    } }
  }

  public partial class jopt {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements());
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.ZeroCopy.Deserializer.Core.jopt.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class ValueParser {
    private static readonly Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>> _TYPE = new Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>>(Std.JSON.Utils.Parsers.SubParser<Std.JSON.Grammar._IValue, Std.JSON.Errors._IDeserializationError>.Default());
    public static Dafny.TypeDescriptor<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>> _TypeDescriptor() {
      return _TYPE;
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Core
namespace Std.JSON.ZeroCopy.Deserializer.Strings {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> StringBody(Std.JSON.Utils.Cursors._ICursor__ cs)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.Default(Std.JSON.Utils.Cursors.Cursor.Default());
      bool _0_escaped;
      _0_escaped = false;
      uint _hi0 = (cs).dtor_end;
      for (uint _1_point_k = (cs).dtor_point; _1_point_k < _hi0; _1_point_k++) {
        byte _2_byte;
        _2_byte = ((cs).dtor_s).Select(_1_point_k);
        if (((_2_byte) == ((byte)((new Dafny.Rune('\"')).Value))) && (!(_0_escaped))) {
          pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Cursor__.create((cs).dtor_s, (cs).dtor_beg, _1_point_k, (cs).dtor_end));
          return pr;
        } else if ((_2_byte) == ((byte)((new Dafny.Rune('\\')).Value))) {
          _0_escaped = !(_0_escaped);
        } else {
          _0_escaped = false;
        }
      }
      pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_EOF());
      return pr;
      return pr;
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Quote(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertChar<Std.JSON.Errors._IDeserializationError>(new Dafny.Rune('\"'));
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> String(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ICursor__ _0_origCs = cs;
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _1_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Strings.__default.Quote(cs);
      if ((_1_valueOrError0).IsFailure()) {
        return (_1_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs0 = (_1_valueOrError0).Extract();
        Std.JSON.Utils.Views.Core._IView__ _2_lq = _let_tmp_rhs0.dtor_t;
        Std.JSON.Utils.Cursors._ICursor__ _3_cs = _let_tmp_rhs0.dtor_cs;
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _4_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Strings.__default.StringBody(_3_cs);
        if ((_4_valueOrError1).IsFailure()) {
          return (_4_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>>();
        } else {
          Std.JSON.Utils.Cursors._ICursor__ _5_contents = (_4_valueOrError1).Extract();
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs1 = (_5_contents).Split();
          Std.JSON.Utils.Views.Core._IView__ _6_contents = _let_tmp_rhs1.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _7_cs = _let_tmp_rhs1.dtor_cs;
          Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _8_valueOrError2 = Std.JSON.ZeroCopy.Deserializer.Strings.__default.Quote(_7_cs);
          if ((_8_valueOrError2).IsFailure()) {
            return (_8_valueOrError2).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>>();
          } else {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs2 = (_8_valueOrError2).Extract();
            Std.JSON.Utils.Views.Core._IView__ _9_rq = _let_tmp_rhs2.dtor_t;
            Std.JSON.Utils.Cursors._ICursor__ _10_cs = _let_tmp_rhs2.dtor_cs;
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring> _11_result = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._Ijstring>.create(Std.JSON.Grammar.jstring.create(_2_lq, _6_contents, _9_rq), _10_cs);
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_11_result);
          }
        }
      }
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Strings
namespace Std.JSON.ZeroCopy.Deserializer.Numbers {

  public partial class __default {
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> Digits(Std.JSON.Utils.Cursors._ICursor__ cs) {
      return ((cs).SkipWhile(Std.JSON.Grammar.__default.Digit_q)).Split();
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> NonEmptyDigits(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _0_sp = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.Digits(cs);
      if (((_0_sp).dtor_t).Empty_q) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_OtherError(Std.JSON.Errors.DeserializationError.create_EmptyNumber()));
      } else {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_0_sp);
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> NonZeroInt(Std.JSON.Utils.Cursors._ICursor__ cs) {
      return Std.JSON.ZeroCopy.Deserializer.Numbers.__default.NonEmptyDigits(cs);
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> OptionalMinus(Std.JSON.Utils.Cursors._ICursor__ cs) {
      return ((cs).SkipIf(((System.Func<byte, bool>)((_0_c) => {
        return (_0_c) == ((byte)((new Dafny.Rune('-')).Value));
      })))).Split();
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> OptionalSign(Std.JSON.Utils.Cursors._ICursor__ cs) {
      return ((cs).SkipIf(((System.Func<byte, bool>)((_0_c) => {
        return ((_0_c) == ((byte)((new Dafny.Rune('-')).Value))) || ((_0_c) == ((byte)((new Dafny.Rune('+')).Value)));
      })))).Split();
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> TrimmedInt(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _0_sp = ((cs).SkipIf(((System.Func<byte, bool>)((_1_c) => {
        return (_1_c) == ((byte)((new Dafny.Rune('0')).Value));
      })))).Split();
      if (((_0_sp).dtor_t).Empty_q) {
        return Std.JSON.ZeroCopy.Deserializer.Numbers.__default.NonZeroInt((_0_sp).dtor_cs);
      } else {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_0_sp);
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Exp(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs0 = ((cs).SkipIf(((System.Func<byte, bool>)((_0_c) => {
        return ((_0_c) == ((byte)((new Dafny.Rune('e')).Value))) || ((_0_c) == ((byte)((new Dafny.Rune('E')).Value)));
      })))).Split();
      Std.JSON.Utils.Views.Core._IView__ _1_e = _let_tmp_rhs0.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _2_cs = _let_tmp_rhs0.dtor_cs;
      if ((_1_e).Empty_q) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>.create(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijexp>.create_Empty(), _2_cs));
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs1 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.OptionalSign(_2_cs);
        Std.JSON.Utils.Views.Core._IView__ _3_sign = _let_tmp_rhs1.dtor_t;
        Std.JSON.Utils.Cursors._ICursor__ _4_cs = _let_tmp_rhs1.dtor_cs;
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _5_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.NonEmptyDigits(_4_cs);
        if ((_5_valueOrError0).IsFailure()) {
          return (_5_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs2 = (_5_valueOrError0).Extract();
          Std.JSON.Utils.Views.Core._IView__ _6_num = _let_tmp_rhs2.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _7_cs = _let_tmp_rhs2.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>.create(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijexp>.create_NonEmpty(Std.JSON.Grammar.jexp.create(_1_e, _3_sign, _6_num)), _7_cs));
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Frac(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs0 = ((cs).SkipIf(((System.Func<byte, bool>)((_0_c) => {
        return (_0_c) == ((byte)((new Dafny.Rune('.')).Value));
      })))).Split();
      Std.JSON.Utils.Views.Core._IView__ _1_period = _let_tmp_rhs0.dtor_t;
      Std.JSON.Utils.Cursors._ICursor__ _2_cs = _let_tmp_rhs0.dtor_cs;
      if ((_1_period).Empty_q) {
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>.create(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijfrac>.create_Empty(), _2_cs));
      } else {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _3_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.NonEmptyDigits(_2_cs);
        if ((_3_valueOrError0).IsFailure()) {
          return (_3_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs1 = (_3_valueOrError0).Extract();
          Std.JSON.Utils.Views.Core._IView__ _4_num = _let_tmp_rhs1.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _5_cs = _let_tmp_rhs1.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>.create(Std.JSON.Grammar.Maybe<Std.JSON.Grammar._Ijfrac>.create_NonEmpty(Std.JSON.Grammar.jfrac.create(_1_period, _4_num)), _5_cs));
        }
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber> NumberFromParts(Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> minus, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> num, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>> frac, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>> exp)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber> _0_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._Ijnumber>.create(Std.JSON.Grammar.jnumber.create((minus).dtor_t, (num).dtor_t, (frac).dtor_t, (exp).dtor_t), (exp).dtor_cs);
      return _0_sp;
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Number(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _0_minus = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.OptionalMinus(cs);
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _1_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.TrimmedInt((_0_minus).dtor_cs);
      if ((_1_valueOrError0).IsFailure()) {
        return (_1_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _2_num = (_1_valueOrError0).Extract();
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _3_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.Frac((_2_num).dtor_cs);
        if ((_3_valueOrError1).IsFailure()) {
          return (_3_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijfrac>> _4_frac = (_3_valueOrError1).Extract();
          Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _5_valueOrError2 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.Exp((_4_frac).dtor_cs);
          if ((_5_valueOrError2).IsFailure()) {
            return (_5_valueOrError2).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>>();
          } else {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IMaybe<Std.JSON.Grammar._Ijexp>> _6_exp = (_5_valueOrError2).Extract();
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.ZeroCopy.Deserializer.Numbers.__default.NumberFromParts(_0_minus, _2_num, _4_frac, _6_exp));
          }
        }
      }
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Numbers
namespace Std.JSON.ZeroCopy.Deserializer.ObjectParams {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Colon(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertChar<Std.JSON.Errors._IDeserializationError>(new Dafny.Rune(':'));
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> KeyValueFromParts(Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring> k, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> colon, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> v)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> _0_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IjKeyValue>.create(Std.JSON.Grammar.jKeyValue.create((k).dtor_t, (colon).dtor_t, (v).dtor_t), (v).dtor_cs);
      return _0_sp;
    }
    public static Dafny.ISequence<byte> ElementSpec(Std.JSON.Grammar._IjKeyValue t) {
      return Std.JSON.ConcreteSyntax.Spec.__default.KeyValue(t);
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Element(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Strings.__default.String(cs);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring> _1_k = (_0_valueOrError0).Extract();
        Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _2_p = Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.Colon;
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _3_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Utils.Views.Core._IView__>((_1_k).dtor_cs, _2_p);
        if ((_3_valueOrError1).IsFailure()) {
          return (_3_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _4_colon = (_3_valueOrError1).Extract();
          Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _5_valueOrError2 = Dafny.Helpers.Id<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>>((json))((_4_colon).dtor_cs);
          if ((_5_valueOrError2).IsFailure()) {
            return (_5_valueOrError2).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>>();
          } else {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> _6_v = (_5_valueOrError2).Extract();
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> _7_kv = Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.KeyValueFromParts(_1_k, _4_colon, _6_v);
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_7_kv);
          }
        }
      }
    }
    public static byte OPEN { get {
      return (byte)((new Dafny.Rune('{')).Value);
    } }
    public static byte CLOSE { get {
      return (byte)((new Dafny.Rune('}')).Value);
    } }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.ObjectParams
namespace Std.JSON.ZeroCopy.Deserializer.Objects {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Object(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Objects.__default.Bracketed(cs, json);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _1_sp = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_1_sp);
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Open(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertByte<Std.JSON.Errors._IDeserializationError>(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.OPEN);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Close(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertByte<Std.JSON.Errors._IDeserializationError>(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.CLOSE);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> BracketedFromParts(Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> open, Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> close)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _0_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>.create(Std.JSON.Grammar.Bracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>.create((open).dtor_t, (elems).dtor_t, (close).dtor_t), (close).dtor_cs);
      return _0_sp;
    }
    public static Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> AppendWithSuffix(Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> elem, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> sep)
    {
      Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__> _0_suffixed = Std.JSON.Grammar.Suffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>.create((elem).dtor_t, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>.create_NonEmpty((sep).dtor_t));
      Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _1_elems_k = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>.Concat((elems).dtor_t, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(_0_suffixed)), (sep).dtor_cs);
      return _1_elems_k;
    }
    public static Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> AppendLast(Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> elem, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> sep)
    {
      Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__> _0_suffixed = Std.JSON.Grammar.Suffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>.create((elem).dtor_t, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>.create_Empty());
      Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _1_elems_k = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>.Concat((elems).dtor_t, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(_0_suffixed)), (elem).dtor_cs);
      return _1_elems_k;
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Elements(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> open, Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> elems)
    {
    TAIL_CALL_START: ;
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.Element((elems).dtor_cs, json);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IjKeyValue> _1_elem = (_0_valueOrError0).Extract();
        if (((_1_elem).dtor_cs).EOF_q) {
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_EOF());
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _2_sep = Std.JSON.ZeroCopy.Deserializer.Core.__default.TryStructural((_1_elem).dtor_cs);
          short _3_s0 = (((_2_sep).dtor_t).dtor_t).Peek();
          if (((_3_s0) == ((short)(Std.JSON.ZeroCopy.Deserializer.Objects.__default.SEPARATOR))) && (((((_2_sep).dtor_t).dtor_t).Length()) == (1U))) {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _4_sep = _2_sep;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _5_elems = Std.JSON.ZeroCopy.Deserializer.Objects.__default.AppendWithSuffix(elems, _1_elem, _4_sep);
            Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _in0 = json;
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _in1 = open;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _in2 = _5_elems;
            json = _in0;
            open = _in1;
            elems = _in2;
            goto TAIL_CALL_START;
          } else if (((_3_s0) == ((short)(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.CLOSE))) && (((((_2_sep).dtor_t).dtor_t).Length()) == (1U))) {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _6_sep = _2_sep;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _7_elems_k = Std.JSON.ZeroCopy.Deserializer.Objects.__default.AppendLast(elems, _1_elem, _6_sep);
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _8_bracketed = Std.JSON.ZeroCopy.Deserializer.Objects.__default.BracketedFromParts(open, _7_elems_k, _6_sep);
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_8_bracketed);
          } else {
            byte _9_separator = Std.JSON.ZeroCopy.Deserializer.Objects.__default.SEPARATOR;
            Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _10_pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_ExpectingAnyByte(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.CLOSE, _9_separator), _3_s0));
            return _10_pr;
          }
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Bracketed(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Utils.Views.Core._IView__>(cs, Std.JSON.ZeroCopy.Deserializer.Objects.__default.Open);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _1_open = (_0_valueOrError0).Extract();
        Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>> _2_elems = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(), (_1_open).dtor_cs);
        if ((((_1_open).dtor_cs).Peek()) == ((short)(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.CLOSE))) {
          Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _3_p = Std.JSON.ZeroCopy.Deserializer.Objects.__default.Close;
          Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _4_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Utils.Views.Core._IView__>((_1_open).dtor_cs, _3_p);
          if ((_4_valueOrError1).IsFailure()) {
            return (_4_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
          } else {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _5_close = (_4_valueOrError1).Extract();
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.ZeroCopy.Deserializer.Objects.__default.BracketedFromParts(_1_open, _2_elems, _5_close));
          }
        } else {
          return Std.JSON.ZeroCopy.Deserializer.Objects.__default.Elements(json, _1_open, _2_elems);
        }
      }
    }
    public static Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>> SpecViewOpen { get {
      return Std.JSON.ZeroCopy.Deserializer.Core.__default.SpecView;
    } }
    public static Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>> SpecViewClose { get {
      return Std.JSON.ZeroCopy.Deserializer.Core.__default.SpecView;
    } }
    public static byte SEPARATOR { get {
      return (byte)((new Dafny.Rune(',')).Value);
    } }
  }

  public partial class jopen {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.OPEN));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.ZeroCopy.Deserializer.Objects.jopen.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jclose {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ObjectParams.__default.CLOSE));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.ZeroCopy.Deserializer.Objects.jclose.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Objects
namespace Std.JSON.ZeroCopy.Deserializer.ArrayParams {

  public partial class __default {
    public static Dafny.ISequence<byte> ElementSpec(Std.JSON.Grammar._IValue t) {
      return Std.JSON.ConcreteSyntax.Spec.__default.Value(t);
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Element(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      return Dafny.Helpers.Id<Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>>((json))(cs);
    }
    public static byte OPEN { get {
      return (byte)((new Dafny.Rune('[')).Value);
    } }
    public static byte CLOSE { get {
      return (byte)((new Dafny.Rune(']')).Value);
    } }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.ArrayParams
namespace Std.JSON.ZeroCopy.Deserializer.Arrays {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Array(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.Bracketed(cs, json);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _1_sp = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_1_sp);
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Open(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertByte<Std.JSON.Errors._IDeserializationError>(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.OPEN);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Close(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertByte<Std.JSON.Errors._IDeserializationError>(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.CLOSE);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
    public static Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> BracketedFromParts(Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> open, Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> close)
    {
      Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _0_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>.create(Std.JSON.Grammar.Bracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>.create((open).dtor_t, (elems).dtor_t, (close).dtor_t), (close).dtor_cs);
      return _0_sp;
    }
    public static Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> AppendWithSuffix(Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> elem, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> sep)
    {
      Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__> _0_suffixed = Std.JSON.Grammar.Suffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>.create((elem).dtor_t, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>.create_NonEmpty((sep).dtor_t));
      Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _1_elems_k = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>.Concat((elems).dtor_t, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(_0_suffixed)), (sep).dtor_cs);
      return _1_elems_k;
    }
    public static Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> AppendLast(Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> elems, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> elem, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> sep)
    {
      Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__> _0_suffixed = Std.JSON.Grammar.Suffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>.create((elem).dtor_t, Std.JSON.Grammar.Maybe<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>.create_Empty());
      Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _1_elems_k = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>.Concat((elems).dtor_t, Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(_0_suffixed)), (elem).dtor_cs);
      return _1_elems_k;
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Elements(Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json, Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> open, Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> elems)
    {
    TAIL_CALL_START: ;
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.Element((elems).dtor_cs, json);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> _1_elem = (_0_valueOrError0).Extract();
        if (((_1_elem).dtor_cs).EOF_q) {
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_EOF());
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _2_sep = Std.JSON.ZeroCopy.Deserializer.Core.__default.TryStructural((_1_elem).dtor_cs);
          short _3_s0 = (((_2_sep).dtor_t).dtor_t).Peek();
          if (((_3_s0) == ((short)(Std.JSON.ZeroCopy.Deserializer.Arrays.__default.SEPARATOR))) && (((((_2_sep).dtor_t).dtor_t).Length()) == (1U))) {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _4_sep = _2_sep;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _5_elems = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.AppendWithSuffix(elems, _1_elem, _4_sep);
            Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _in0 = json;
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _in1 = open;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _in2 = _5_elems;
            json = _in0;
            open = _in1;
            elems = _in2;
            goto TAIL_CALL_START;
          } else if (((_3_s0) == ((short)(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.CLOSE))) && (((((_2_sep).dtor_t).dtor_t).Length()) == (1U))) {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _6_sep = _2_sep;
            Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _7_elems_k = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.AppendLast(elems, _1_elem, _6_sep);
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _8_bracketed = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.BracketedFromParts(open, _7_elems_k, _6_sep);
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_8_bracketed);
          } else {
            byte _9_separator = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.SEPARATOR;
            Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _10_pr = Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Failure(Std.JSON.Utils.Cursors.CursorError<Std.JSON.Errors._IDeserializationError>.create_ExpectingAnyByte(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.CLOSE, _9_separator), _3_s0));
            return _10_pr;
          }
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Bracketed(Std.JSON.Utils.Cursors._ICursor__ cs, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> json)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Utils.Views.Core._IView__>(cs, Std.JSON.ZeroCopy.Deserializer.Arrays.__default.Open);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _1_open = (_0_valueOrError0).Extract();
        Std.JSON.Utils.Cursors._ISplit<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>> _2_elems = Std.JSON.Utils.Cursors.Split<Dafny.ISequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>>.create(Dafny.Sequence<Std.JSON.Grammar._ISuffixed<Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__>>.FromElements(), (_1_open).dtor_cs);
        if ((((_1_open).dtor_cs).Peek()) == ((short)(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.CLOSE))) {
          Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _3_p = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.Close;
          Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _4_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Utils.Views.Core._IView__>((_1_open).dtor_cs, _3_p);
          if ((_4_valueOrError1).IsFailure()) {
            return (_4_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>>();
          } else {
            Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Utils.Views.Core._IView__>> _5_close = (_4_valueOrError1).Extract();
            return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.ZeroCopy.Deserializer.Arrays.__default.BracketedFromParts(_1_open, _2_elems, _5_close));
          }
        } else {
          return Std.JSON.ZeroCopy.Deserializer.Arrays.__default.Elements(json, _1_open, _2_elems);
        }
      }
    }
    public static Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>> SpecViewOpen { get {
      return Std.JSON.ZeroCopy.Deserializer.Core.__default.SpecView;
    } }
    public static Func<Std.JSON.Utils.Views.Core._IView__, Dafny.ISequence<byte>> SpecViewClose { get {
      return Std.JSON.ZeroCopy.Deserializer.Core.__default.SpecView;
    } }
    public static byte SEPARATOR { get {
      return (byte)((new Dafny.Rune(',')).Value);
    } }
  }

  public partial class jopen {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.OPEN));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.ZeroCopy.Deserializer.Arrays.jopen.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }

  public partial class jclose {
    private static readonly Std.JSON.Utils.Views.Core._IView__ Witness = Std.JSON.Utils.Views.Core.View__.OfBytes(Dafny.Sequence<byte>.FromElements(Std.JSON.ZeroCopy.Deserializer.ArrayParams.__default.CLOSE));
    public static Std.JSON.Utils.Views.Core._IView__ Default() {
      return Witness;
    }
    private static readonly Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TYPE = new Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__>(Std.JSON.ZeroCopy.Deserializer.Arrays.jclose.Default());
    public static Dafny.TypeDescriptor<Std.JSON.Utils.Views.Core._IView__> _TypeDescriptor() {
      return _TYPE;
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Arrays
namespace Std.JSON.ZeroCopy.Deserializer.Constants {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Constant(Std.JSON.Utils.Cursors._ICursor__ cs, Dafny.ISequence<byte> expected)
    {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ICursor__, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _0_valueOrError0 = (cs).AssertBytes<Std.JSON.Errors._IDeserializationError>(expected, 0U);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>>();
      } else {
        Std.JSON.Utils.Cursors._ICursor__ _1_cs = (_0_valueOrError0).Extract();
        return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success((_1_cs).Split());
      }
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Constants
namespace Std.JSON.ZeroCopy.Deserializer.Values {

  public partial class __default {
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> Value(Std.JSON.Utils.Cursors._ICursor__ cs) {
      short _0_c = (cs).Peek();
      if ((_0_c) == ((short)((new Dafny.Rune('{')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _1_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.Objects.__default.Object(cs, Std.JSON.ZeroCopy.Deserializer.Values.__default.ValueParser(cs));
        if ((_1_valueOrError0).IsFailure()) {
          return (_1_valueOrError0).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _let_tmp_rhs0 = (_1_valueOrError0).Extract();
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IjKeyValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _2_obj = _let_tmp_rhs0.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _3_cs_k = _let_tmp_rhs0.dtor_cs;
          Std.JSON.Grammar._IValue _4_v = Std.JSON.Grammar.Value.create_Object(_2_obj);
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> _5_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(_4_v, _3_cs_k);
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_5_sp);
        }
      } else if ((_0_c) == ((short)((new Dafny.Rune('[')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _6_valueOrError1 = Std.JSON.ZeroCopy.Deserializer.Arrays.__default.Array(cs, Std.JSON.ZeroCopy.Deserializer.Values.__default.ValueParser(cs));
        if ((_6_valueOrError1).IsFailure()) {
          return (_6_valueOrError1).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__>> _let_tmp_rhs1 = (_6_valueOrError1).Extract();
          Std.JSON.Grammar._IBracketed<Std.JSON.Utils.Views.Core._IView__, Std.JSON.Grammar._IValue, Std.JSON.Utils.Views.Core._IView__, Std.JSON.Utils.Views.Core._IView__> _7_arr = _let_tmp_rhs1.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _8_cs_k = _let_tmp_rhs1.dtor_cs;
          Std.JSON.Grammar._IValue _9_v = Std.JSON.Grammar.Value.create_Array(_7_arr);
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> _10_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(_9_v, _8_cs_k);
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_10_sp);
        }
      } else if ((_0_c) == ((short)((new Dafny.Rune('\"')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _11_valueOrError2 = Std.JSON.ZeroCopy.Deserializer.Strings.__default.String(cs);
        if ((_11_valueOrError2).IsFailure()) {
          return (_11_valueOrError2).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijstring> _let_tmp_rhs2 = (_11_valueOrError2).Extract();
          Std.JSON.Grammar._Ijstring _12_str = _let_tmp_rhs2.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _13_cs_k = _let_tmp_rhs2.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(Std.JSON.Grammar.Value.create_String(_12_str), _13_cs_k));
        }
      } else if ((_0_c) == ((short)((new Dafny.Rune('t')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _14_valueOrError3 = Std.JSON.ZeroCopy.Deserializer.Constants.__default.Constant(cs, Std.JSON.Grammar.__default.TRUE);
        if ((_14_valueOrError3).IsFailure()) {
          return (_14_valueOrError3).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs3 = (_14_valueOrError3).Extract();
          Std.JSON.Utils.Views.Core._IView__ _15_cst = _let_tmp_rhs3.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _16_cs_k = _let_tmp_rhs3.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(Std.JSON.Grammar.Value.create_Bool(_15_cst), _16_cs_k));
        }
      } else if ((_0_c) == ((short)((new Dafny.Rune('f')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _17_valueOrError4 = Std.JSON.ZeroCopy.Deserializer.Constants.__default.Constant(cs, Std.JSON.Grammar.__default.FALSE);
        if ((_17_valueOrError4).IsFailure()) {
          return (_17_valueOrError4).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs4 = (_17_valueOrError4).Extract();
          Std.JSON.Utils.Views.Core._IView__ _18_cst = _let_tmp_rhs4.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _19_cs_k = _let_tmp_rhs4.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(Std.JSON.Grammar.Value.create_Bool(_18_cst), _19_cs_k));
        }
      } else if ((_0_c) == ((short)((new Dafny.Rune('n')).Value))) {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _20_valueOrError5 = Std.JSON.ZeroCopy.Deserializer.Constants.__default.Constant(cs, Std.JSON.Grammar.__default.NULL);
        if ((_20_valueOrError5).IsFailure()) {
          return (_20_valueOrError5).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Utils.Views.Core._IView__> _let_tmp_rhs5 = (_20_valueOrError5).Extract();
          Std.JSON.Utils.Views.Core._IView__ _21_cst = _let_tmp_rhs5.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _22_cs_k = _let_tmp_rhs5.dtor_cs;
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(Std.JSON.Grammar.Value.create_Null(_21_cst), _22_cs_k));
        }
      } else {
        Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>> _23_valueOrError6 = Std.JSON.ZeroCopy.Deserializer.Numbers.__default.Number(cs);
        if ((_23_valueOrError6).IsFailure()) {
          return (_23_valueOrError6).PropagateFailure<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>>();
        } else {
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._Ijnumber> _let_tmp_rhs6 = (_23_valueOrError6).Extract();
          Std.JSON.Grammar._Ijnumber _24_num = _let_tmp_rhs6.dtor_t;
          Std.JSON.Utils.Cursors._ICursor__ _25_cs_k = _let_tmp_rhs6.dtor_cs;
          Std.JSON.Grammar._IValue _26_v = Std.JSON.Grammar.Value.create_Number(_24_num);
          Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue> _27_sp = Std.JSON.Utils.Cursors.Split<Std.JSON.Grammar._IValue>.create(_26_v, _25_cs_k);
          return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.create_Success(_27_sp);
        }
      }
    }
    public static Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> ValueParser(Std.JSON.Utils.Cursors._ICursor__ cs) {
      Func<Std.JSON.Utils.Cursors._ICursor__, bool> _0_pre = Dafny.Helpers.Id<Func<Std.JSON.Utils.Cursors._ICursor__, Func<Std.JSON.Utils.Cursors._ICursor__, bool>>>((_1_cs) => ((System.Func<Std.JSON.Utils.Cursors._ICursor__, bool>)((_2_ps_k) => {
        return ((_2_ps_k).Length()) < ((_1_cs).Length());
      })))(cs);
      Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>> _3_fn = Dafny.Helpers.Id<Func<Func<Std.JSON.Utils.Cursors._ICursor__, bool>, Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>>>((_4_pre) => ((System.Func<Std.JSON.Utils.Cursors._ICursor__, Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IValue>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>>)((_5_ps_k) => {
        return Std.JSON.ZeroCopy.Deserializer.Values.__default.Value(_5_ps_k);
      })))(_0_pre);
      return _3_fn;
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.Values
namespace Std.JSON.ZeroCopy.Deserializer.API {

  public partial class __default {
    public static Std.JSON.Errors._IDeserializationError LiftCursorError(Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError> err) {
      Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError> _source0 = err;
      {
        if (_source0.is_EOF) {
          return Std.JSON.Errors.DeserializationError.create_ReachedEOF();
        }
      }
      {
        if (_source0.is_ExpectingByte) {
          byte _0_expected = _source0.dtor_expected;
          short _1_b = _source0.dtor_b;
          return Std.JSON.Errors.DeserializationError.create_ExpectingByte(_0_expected, _1_b);
        }
      }
      {
        if (_source0.is_ExpectingAnyByte) {
          Dafny.ISequence<byte> _2_expected__sq = _source0.dtor_expected__sq;
          short _3_b = _source0.dtor_b;
          return Std.JSON.Errors.DeserializationError.create_ExpectingAnyByte(_2_expected__sq, _3_b);
        }
      }
      {
        Std.JSON.Errors._IDeserializationError _4_err = _source0.dtor_err;
        return _4_err;
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>, Std.JSON.Errors._IDeserializationError> JSON(Std.JSON.Utils.Cursors._ICursor__ cs) {
      return Std.Wrappers.Result<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>, Std.JSON.Utils.Cursors._ICursorError<Std.JSON.Errors._IDeserializationError>>.MapFailure<Std.JSON.Errors._IDeserializationError>(Std.JSON.ZeroCopy.Deserializer.Core.__default.Structural<Std.JSON.Grammar._IValue>(cs, Std.JSON.ZeroCopy.Deserializer.Values.__default.Value), Std.JSON.ZeroCopy.Deserializer.API.__default.LiftCursorError);
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._IDeserializationError> Text(Std.JSON.Utils.Views.Core._IView__ v) {
      Std.Wrappers._IResult<Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>, Std.JSON.Errors._IDeserializationError> _0_valueOrError0 = Std.JSON.ZeroCopy.Deserializer.API.__default.JSON(Std.JSON.Utils.Cursors.Cursor__.OfView(v));
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>();
      } else {
        Std.JSON.Utils.Cursors._ISplit<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>> _let_tmp_rhs0 = (_0_valueOrError0).Extract();
        Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> _1_text = _let_tmp_rhs0.dtor_t;
        Std.JSON.Utils.Cursors._ICursor__ _2_cs = _let_tmp_rhs0.dtor_cs;
        Std.Wrappers._IOutcomeResult<Std.JSON.Errors._IDeserializationError> _3_valueOrError1 = Std.Wrappers.__default.Need<Std.JSON.Errors._IDeserializationError>((_2_cs).EOF_q, Std.JSON.Errors.DeserializationError.create_ExpectingEOF());
        if ((_3_valueOrError1).IsFailure()) {
          return (_3_valueOrError1).PropagateFailure<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>();
        } else {
          return Std.Wrappers.Result<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._IDeserializationError>.create_Success(_1_text);
        }
      }
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._IDeserializationError> OfBytes(Dafny.ISequence<byte> bs) {
      Std.Wrappers._IOutcomeResult<Std.JSON.Errors._IDeserializationError> _0_valueOrError0 = Std.Wrappers.__default.Need<Std.JSON.Errors._IDeserializationError>((new BigInteger((bs).Count)) < (Std.BoundedInts.__default.TWO__TO__THE__32), Std.JSON.Errors.DeserializationError.create_IntOverflow());
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>>();
      } else {
        return Std.JSON.ZeroCopy.Deserializer.API.__default.Text(Std.JSON.Utils.Views.Core.View__.OfBytes(bs));
      }
    }
  }
} // end of namespace Std.JSON.ZeroCopy.Deserializer.API
namespace Std.JSON.ZeroCopy.Deserializer {

} // end of namespace Std.JSON.ZeroCopy.Deserializer
namespace Std.JSON.ZeroCopy.API {

  public partial class __default {
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Serialize(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js) {
      return Std.Wrappers.Result<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError>.create_Success((Std.JSON.ZeroCopy.Serializer.__default.Text(js)).Bytes());
    }
    public static Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> SerializeAlloc(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js)
    {
      Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> bs = Std.Wrappers.Result<byte[], Std.JSON.Errors._ISerializationError>.Default(new byte[0]);
      Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> _out0;
      _out0 = Std.JSON.ZeroCopy.Serializer.__default.Serialize(js);
      bs = _out0;
      return bs;
    }
    public static Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> SerializeInto(Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> js, byte[] bs)
    {
      Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> len = Std.Wrappers.Result<uint, Std.JSON.Errors._ISerializationError>.Default(0);
      Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> _out0;
      _out0 = Std.JSON.ZeroCopy.Serializer.__default.SerializeTo(js, bs);
      len = _out0;
      return len;
    }
    public static Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._IDeserializationError> Deserialize(Dafny.ISequence<byte> bs) {
      return Std.JSON.ZeroCopy.Deserializer.API.__default.OfBytes(bs);
    }
  }
} // end of namespace Std.JSON.ZeroCopy.API
namespace Std.JSON.API {

  public partial class __default {
    public static Std.Wrappers._IResult<Dafny.ISequence<byte>, Std.JSON.Errors._ISerializationError> Serialize(Std.JSON.Values._IJSON js) {
      Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.JSON.Serializer.__default.JSON(js);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Dafny.ISequence<byte>>();
      } else {
        Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> _1_js = (_0_valueOrError0).Extract();
        return Std.JSON.ZeroCopy.API.__default.Serialize(_1_js);
      }
    }
    public static Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> SerializeAlloc(Std.JSON.Values._IJSON js)
    {
      Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> bs = Std.Wrappers.Result<byte[], Std.JSON.Errors._ISerializationError>.Default(new byte[0]);
      Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.Wrappers.Result<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError>.Default(Std.JSON.Grammar.Structural<Std.JSON.Grammar._IValue>.Default(Std.JSON.Grammar.Value.Default()));
      _0_valueOrError0 = Std.JSON.Serializer.__default.JSON(js);
      if ((_0_valueOrError0).IsFailure()) {
        bs = (_0_valueOrError0).PropagateFailure<byte[]>();
        return bs;
      }
      Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> _1_js;
      _1_js = (_0_valueOrError0).Extract();
      Std.Wrappers._IResult<byte[], Std.JSON.Errors._ISerializationError> _out0;
      _out0 = Std.JSON.ZeroCopy.API.__default.SerializeAlloc(_1_js);
      bs = _out0;
      return bs;
    }
    public static Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> SerializeInto(Std.JSON.Values._IJSON js, byte[] bs)
    {
      Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> len = Std.Wrappers.Result<uint, Std.JSON.Errors._ISerializationError>.Default(0);
      Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError> _0_valueOrError0 = Std.Wrappers.Result<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._ISerializationError>.Default(Std.JSON.Grammar.Structural<Std.JSON.Grammar._IValue>.Default(Std.JSON.Grammar.Value.Default()));
      _0_valueOrError0 = Std.JSON.Serializer.__default.JSON(js);
      if ((_0_valueOrError0).IsFailure()) {
        len = (_0_valueOrError0).PropagateFailure<uint>();
        return len;
      }
      Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> _1_js;
      _1_js = (_0_valueOrError0).Extract();
      Std.Wrappers._IResult<uint, Std.JSON.Errors._ISerializationError> _out0;
      _out0 = Std.JSON.ZeroCopy.API.__default.SerializeInto(_1_js, bs);
      len = _out0;
      return len;
    }
    public static Std.Wrappers._IResult<Std.JSON.Values._IJSON, Std.JSON.Errors._IDeserializationError> Deserialize(Dafny.ISequence<byte> bs) {
      Std.Wrappers._IResult<Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue>, Std.JSON.Errors._IDeserializationError> _0_valueOrError0 = Std.JSON.ZeroCopy.API.__default.Deserialize(bs);
      if ((_0_valueOrError0).IsFailure()) {
        return (_0_valueOrError0).PropagateFailure<Std.JSON.Values._IJSON>();
      } else {
        Std.JSON.Grammar._IStructural<Std.JSON.Grammar._IValue> _1_js = (_0_valueOrError0).Extract();
        return Std.JSON.Deserializer.__default.JSON(_1_js);
      }
    }
  }
} // end of namespace Std.JSON.API
namespace _module {

  public partial class __default {
    public static bool check__greater(Dafny.ISequence<BigInteger> arr, BigInteger number)
    {
      bool res = false;
      Dafny.ISequence<BigInteger> _0_sortedArr;
      _0_sortedArr = arr;
      res = (number) > ((_0_sortedArr).Select((new BigInteger((_0_sortedArr).Count)) - (BigInteger.One)));
      return res;
    }
    public static void check()
    {
      bool _0_call0;
      bool _out0;
      _out0 = __default.check__greater(Dafny.Sequence<BigInteger>.FromElements(BigInteger.One, new BigInteger(2), new BigInteger(3), new BigInteger(4), new BigInteger(5)), new BigInteger(6));
      _0_call0 = _out0;
      bool _1_call1;
      bool _out1;
      _out1 = __default.check__greater(Dafny.Sequence<BigInteger>.FromElements(new BigInteger(10), new BigInteger(20), new BigInteger(30), new BigInteger(40), new BigInteger(50)), new BigInteger(25));
      _1_call1 = _out1;
      bool _2_call2;
      bool _out2;
      _out2 = __default.check__greater(Dafny.Sequence<BigInteger>.FromElements(new BigInteger(-10), new BigInteger(-20), new BigInteger(-30), new BigInteger(-40), new BigInteger(-50)), new BigInteger(-5));
      _2_call2 = _out2;
      bool _3_call3;
      bool _out3;
      _out3 = __default.check__greater(Dafny.Sequence<BigInteger>.FromElements(new BigInteger(100), new BigInteger(200), new BigInteger(300), new BigInteger(400), new BigInteger(500)), new BigInteger(600));
      _3_call3 = _out3;
      bool _4_call4;
      bool _out4;
      _out4 = __default.check__greater(Dafny.Sequence<BigInteger>.FromElements(new BigInteger(15), new BigInteger(25), new BigInteger(35), new BigInteger(45), new BigInteger(55)), new BigInteger(60));
      _4_call4 = _out4;
      if (!((_0_call0) == (true))) {
        throw new Dafny.HaltException("output/trans.dfy(18,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString("expectation violation").ToVerbatimString(false));}
      if (!((_1_call1) == (false))) {
        throw new Dafny.HaltException("output/trans.dfy(19,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString("expectation violation").ToVerbatimString(false));}
      if (!((_2_call2) == (true))) {
        throw new Dafny.HaltException("output/trans.dfy(20,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString("expectation violation").ToVerbatimString(false));}
      if (!((_3_call3) == (true))) {
        throw new Dafny.HaltException("output/trans.dfy(21,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString("expectation violation").ToVerbatimString(false));}
      if (!((_4_call4) == (true))) {
        throw new Dafny.HaltException("output/trans.dfy(22,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString("expectation violation").ToVerbatimString(false));}
    }
    public static void __Test____Main__(Dafny.ISequence<Dafny.ISequence<Dafny.Rune>> __noArgsParameter)
    {
      bool _0_success;
      _0_success = true;
      Dafny.Helpers.Print((Dafny.Sequence<Dafny.Rune>.UnicodeFromString(@"check: ")).ToVerbatimString(false));
      try {
        {
          __default.check();
          {
            Dafny.Helpers.Print((Dafny.Sequence<Dafny.Rune>.UnicodeFromString(@"PASSED
")).ToVerbatimString(false));
          }
        }
      }
      catch (Dafny.HaltException e) {
        var _1_haltMessage = Dafny.Sequence<Dafny.Rune>.UnicodeFromString(e.Message);
        {
          Dafny.Helpers.Print((Dafny.Sequence<Dafny.Rune>.UnicodeFromString(@"FAILED
	")).ToVerbatimString(false));
          Dafny.Helpers.Print((_1_haltMessage).ToVerbatimString(false));
          Dafny.Helpers.Print((Dafny.Sequence<Dafny.Rune>.UnicodeFromString(@"
")).ToVerbatimString(false));
          _0_success = false;
        }
      }
      if (!(_0_success)) {
        throw new Dafny.HaltException("output/trans.dfy(1,0): " + Dafny.Sequence<Dafny.Rune>.UnicodeFromString(@"Test failures occurred: see above.
").ToVerbatimString(false));}
    }
  }
} // end of namespace _module
class __CallToMain {
  public static void Main(string[] args) {
    Dafny.Helpers.WithHaltHandling(() => _module.__default.__Test____Main__(Dafny.Sequence<Dafny.ISequence<Dafny.Rune>>.UnicodeFromMainArguments(args)));
  }
}
