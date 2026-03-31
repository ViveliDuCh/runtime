// Correctness tests for the IEnumerable-strategy stream types.
//
// The CRITICAL test: DIM defaults on IStreamSource work correctly
// because the source is IMMUTABLE — no mutable state to lose on boxing.
// Compare with the ISpanOwner-strategy where DIM+boxing = infinite loop.

using System;
using System.IO;

namespace IStreamableBenchmarks;

public static class EnumerableCorrectnessTests
{
    public static int Main(string[] args)
    {
        int failures = 0;

        Console.WriteLine("=== IEnumerable-Strategy Correctness Tests ===");
        Console.WriteLine();

        // ── Test 1: DIM default ReadByteAt works (no infinite loop) ──
        // NOTE: DIMs on structs can only be called through the INTERFACE reference.
        // This means the struct gets boxed — but since the source is READONLY/IMMUTABLE,
        // boxing doesn't lose any mutable state. This is the key fix vs ISpanOwner.
        failures += RunTest("DIM_ReadByteAt_NoInfiniteLoop", () =>
        {
            IStreamSource source = new ReadOnlyMemorySourceMinimal(new byte[] { 1, 2, 3 });

            // ReadByteAt is a DIM default — source is immutable, so boxing is harmless
            Assert(source.ReadByteAt(0) == 1, "ReadByteAt(0) should be 1");
            Assert(source.ReadByteAt(1) == 2, "ReadByteAt(1) should be 2");
            Assert(source.ReadByteAt(2) == 3, "ReadByteAt(2) should be 3");
            Assert(source.ReadByteAt(3) == -1, "ReadByteAt(3) should be -1 (EOF)");
        });

        // ── Test 2: DIM default CreateReader works ──
        failures += RunTest("DIM_CreateReader_Works", () =>
        {
            IStreamSource source = new ReadOnlyMemorySourceMinimal(new byte[] { 10, 20, 30 });

            // CreateReader is a DIM default
            var reader = source.CreateReader();
            Assert(reader != null, "CreateReader should return a reader");
            Assert(reader.Position == 0, "Initial position should be 0");
            Assert(reader.Length == 3, "Length should be 3");
        });

        // ── Test 3: Reader.ReadByte advances position correctly ──
        failures += RunTest("Reader_ReadByte_AdvancesPosition", () =>
        {
            IStreamSource source = new ReadOnlyMemorySourceMinimal(new byte[] { 1, 2, 3 });
            var reader = source.CreateReader();

            Assert(reader.ReadByte() == 1, "First ReadByte should be 1");
            Assert(reader.Position == 1, "Position should be 1 after first read");
            Assert(reader.ReadByte() == 2, "Second ReadByte should be 2");
            Assert(reader.Position == 2, "Position should be 2 after second read");
            Assert(reader.ReadByte() == 3, "Third ReadByte should be 3");
            Assert(reader.Position == 3, "Position should be 3 after third read");
            Assert(reader.ReadByte() == -1, "Fourth ReadByte should be -1 (EOF)");
        });

        // ── Test 4: CRITICAL — DIM-only minimal source through Stream works ──
        // This is the equivalent of the test that FAILED with the ISpanOwner strategy
        failures += RunTest("CRITICAL_DIMOnly_Stream_NoInfiniteLoop", () =>
        {
            IStreamSource source = new ReadOnlyMemorySourceMinimal(new byte[] { 1, 2, 3 });
            using var stream = new EnumerableSourceStream(source);

            // With the ISpanOwner strategy, this produced: 1, 1, 1, 1... (infinite loop)
            // With the IEnumerable strategy, it should produce: 1, 2, 3, -1
            Assert(stream.ReadByte() == 1, "Stream.ReadByte #1 should be 1");
            Assert(stream.ReadByte() == 2, "Stream.ReadByte #2 should be 2");
            Assert(stream.ReadByte() == 3, "Stream.ReadByte #3 should be 3");
            Assert(stream.ReadByte() == -1, "Stream.ReadByte #4 should be -1 (EOF)");
        });

        // ── Test 5: Optimized source works identically ──
        failures += RunTest("Optimized_Stream_ReadByte", () =>
        {
            var source = new ReadOnlyMemorySource(new byte[] { 10, 20, 30, 40 });
            using var stream = new EnumerableSourceStream(source);

            Assert(stream.ReadByte() == 10, "ReadByte #1");
            Assert(stream.ReadByte() == 20, "ReadByte #2");
            Assert(stream.ReadByte() == 30, "ReadByte #3");
            Assert(stream.ReadByte() == 40, "ReadByte #4");
            Assert(stream.ReadByte() == -1, "ReadByte #5 (EOF)");
        });

        // ── Test 6: Bulk Read works ──
        failures += RunTest("Stream_BulkRead", () =>
        {
            byte[] data = { 1, 2, 3, 4, 5 };
            var source = new ReadOnlyMemorySource(data);
            using var stream = new EnumerableSourceStream(source);

            byte[] buf = new byte[3];
            int n = stream.Read(buf);
            Assert(n == 3, $"Should read 3 bytes, got {n}");
            Assert(buf[0] == 1 && buf[1] == 2 && buf[2] == 3, "Bytes should be 1,2,3");
            Assert(stream.Position == 3, "Position should be 3");

            n = stream.Read(buf);
            Assert(n == 2, $"Should read 2 bytes, got {n}");
            Assert(buf[0] == 4 && buf[1] == 5, "Bytes should be 4,5");
        });

        // ── Test 7: Seek works ──
        failures += RunTest("Stream_Seek", () =>
        {
            byte[] data = { 10, 20, 30, 40, 50 };
            var source = new ReadOnlyMemorySource(data);
            using var stream = new EnumerableSourceStream(source);

            stream.Seek(3, SeekOrigin.Begin);
            Assert(stream.ReadByte() == 40, "After Seek(3, Begin), should read 40");

            stream.Seek(-2, SeekOrigin.Current);
            Assert(stream.ReadByte() == 30, "After Seek(-2, Current), should read 30");

            stream.Seek(-1, SeekOrigin.End);
            Assert(stream.ReadByte() == 50, "After Seek(-1, End), should read 50");
        });

        // ── Test 8: CopyTo works ──
        failures += RunTest("Stream_CopyTo", () =>
        {
            byte[] data = { 1, 2, 3, 4, 5 };
            var source = new ReadOnlyMemorySource(data);
            using var stream = new EnumerableSourceStream(source);
            using var dst = new MemoryStream();

            stream.CopyTo(dst);
            byte[] result = dst.ToArray();
            Assert(result.Length == 5, $"CopyTo should copy 5 bytes, got {result.Length}");
            for (int i = 0; i < 5; i++)
                Assert(result[i] == data[i], $"Byte {i} mismatch");
        });

        // ── Test 9: DIM default CopyTo on minimal source works ──
        failures += RunTest("DIM_CopyTo_MinimalSource", () =>
        {
            byte[] data = { 10, 20, 30 };
            IStreamSource source = new ReadOnlyMemorySourceMinimal(data);
            using var stream = new EnumerableSourceStream(source);
            using var dst = new MemoryStream();

            // CopyTo uses the DIM default on the source (rented-buffer loop)
            stream.CopyTo(dst);
            byte[] result = dst.ToArray();
            Assert(result.Length == 3, $"DIM CopyTo should copy 3 bytes, got {result.Length}");
            Assert(result[0] == 10 && result[1] == 20 && result[2] == 30, "Bytes should match");
        });

        // ── Test 10: Writable source ──
        failures += RunTest("Writable_Stream", () =>
        {
            byte[] backing = new byte[5];
            var source = new MemorySource(new Memory<byte>(backing));
            using var stream = new EnumerableSourceStream(source);

            stream.WriteByte(42);
            stream.WriteByte(43);
            stream.Write(new byte[] { 44, 45, 46 });

            Assert(stream.Position == 5, $"Position should be 5, got {stream.Position}");
            Assert(backing[0] == 42, "Byte 0 should be 42");
            Assert(backing[1] == 43, "Byte 1 should be 43");
            Assert(backing[2] == 44, "Byte 2 should be 44");
            Assert(backing[4] == 46, "Byte 4 should be 46");
        });

        // ── Test 11: Compare ISpanOwner DIM vs IEnumerable DIM ──
        failures += RunTest("Compare_DIM_Behavior", () =>
        {
            byte[] data = { 1, 2, 3 };

            // ISpanOwner strategy (DIM-only minimal) — the BROKEN one
            var ispanStream = new StreamableStream<ReadOnlyMemoryStreamableMinimal>(
                new ReadOnlyMemoryStreamableMinimal(data));
            int ispan1 = ispanStream.ReadByte();
            int ispan2 = ispanStream.ReadByte();
            int ispan3 = ispanStream.ReadByte();

            // IEnumerable strategy (DIM-only minimal) — should work
            IStreamSource enumSource = new ReadOnlyMemorySourceMinimal(data);
            using var enumStream = new EnumerableSourceStream(enumSource);
            int enum1 = enumStream.ReadByte();
            int enum2 = enumStream.ReadByte();
            int enum3 = enumStream.ReadByte();
            int enum4 = enumStream.ReadByte();

            Console.WriteLine($"  ISpanOwner DIM-only:  ReadByte() → {ispan1}, {ispan2}, {ispan3}");
            Console.WriteLine($"  IEnumerable DIM-only: ReadByte() → {enum1}, {enum2}, {enum3}, {enum4}");

            // ISpanOwner: all three should be 1 (broken — infinite loop)
            Assert(ispan1 == 1 && ispan2 == 1 && ispan3 == 1,
                $"ISpanOwner DIM should show the bug: 1,1,1 but got {ispan1},{ispan2},{ispan3}");

            // IEnumerable: should progress correctly
            Assert(enum1 == 1 && enum2 == 2 && enum3 == 3 && enum4 == -1,
                $"IEnumerable DIM should work: 1,2,3,-1 but got {enum1},{enum2},{enum3},{enum4}");

            ispanStream.Dispose();
        });

        Console.WriteLine();
        Console.WriteLine($"=== {(failures == 0 ? "ALL TESTS PASSED ✅" : $"{failures} TEST(S) FAILED ❌")} ===");
        return failures;
    }

    private static int RunTest(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"  ✅ {name}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ {name}: {ex.Message}");
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }
}
