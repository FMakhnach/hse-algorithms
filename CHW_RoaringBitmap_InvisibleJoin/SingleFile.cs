using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Program
{
    private static void Main(string[] args)
    {
        try
        {
            //new Tester().Test(1, 5);
            Run(args);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static void Run(params string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("You must specify 1) path to data; 2) path to input file; 3) path to output file.");
            return;
        }
        DBRequester db = new DBRequester(args[0]);
        string[] outputCols;
        using (StreamReader input = new StreamReader(args[1]))
        {
            // The columns that should be in the result.
            outputCols = input.ReadLine().Split(',');
            int n = int.Parse(input.ReadLine());

            // Processing each query.
            for (int i = 0; i < n; ++i)
                db.Query(input.ReadLine());
        }
        // Collecting results.
        List<string> rows = db.GetResults(outputCols);
        using (StreamWriter output = new StreamWriter(args[2]))
        {
            foreach (var row in rows)
            {
                output.Write(row);
                // Idk why, whatever.
                output.Write('\n');
            }
        }
    }
}

#region Bitmaps
/// <summary>
/// Container for sparse chunks of Roaring bitmap (<= 4096 values). 
/// </summary>
public class ArrayContainer : IRoaringBitmapContainer
{
    /// <summary>
    /// Actual values, represented as sorted array.
    /// </summary>
    private readonly ArraySet values;

    /// <summary>
    /// Creates empty array container instance.
    /// </summary>
    public ArrayContainer() => values = new ArraySet();

    /// <summary>
    /// Creates an array container based on 16-bit values array.
    /// </summary>
    /// <param name="array"></param>
    public ArrayContainer(ushort[] array) => values = new ArraySet(array);

    public ArrayContainer(ArrayContainer other) => values = new ArraySet(other.values);

    public bool this[int i]
    {
        get => values.Contains((ushort)i);
        set
        {
            if (value) values.Insert((ushort)i);
            else values.Remove((ushort)i);
        }
    }

    public int Cardinality => values.Cardinality;

    /// <summary>
    /// Performs logical AND with another ArrayContainer instance.
    /// </summary>
    /// <returns> Itself </returns>
    public ArrayContainer And(ArrayContainer other)
    {
        // Just intersecting other values with values of that object.
        values.Intersect(other.values);
        return this;
    }

    /// <summary>
    /// Performs logical AND with BitmapContainer instance.
    /// </summary>
    /// <returns> Itself </returns>
    public ArrayContainer And(BitmapContainer other)
    {
        for (int i = 0; i < values.Cardinality; ++i)
        {
            // If we have no corresponding element in bitmap, 
            // we remove element from this arrayContainer.
            if (!other[values[i]])
            {
                values.RemoveAt(i);
                --i;
            }
        }
        return this;
    }

    public IRoaringBitmapContainer And(IRoaringBitmapContainer other)
    {
        if (other is BitmapContainer) return And((BitmapContainer)other);
        return And((ArrayContainer)other);
    }

    /// <summary>
    /// Rebuilds ArrayContainer to BitmapContainer.
    /// </summary>
    public IRoaringBitmapContainer Rebuild(int newElem)
    {
        BitmapContainer newContainer = new BitmapContainer();
        for (int i = 0; i < values.Cardinality; ++i)
        {
            newContainer[values[i]] = true;
        }
        newContainer[newElem] = true;
        return newContainer;
    }
}

public abstract class Bitmap
{
    /// <summary>
    /// Performs logical "AND" on bitmaps.
    /// </summary>
    public abstract void And(Bitmap other);

    /// <summary>
    /// Sets bit at index <paramref name="i"/> to a <paramref name="value"/>.
    /// </summary>
    public abstract void Set(int i, bool value);

    /// <summary>
    /// Returns bit at index <paramref name="i"/>.
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public abstract bool Get(int i);
}

/// <summary>
/// Container for dense chunks of Roaring bitmap (> 4096 values). 
/// Takes fixed space of 64 * 1024 + 32 = 2^16 + 32 bits.
/// </summary>
public class BitmapContainer : IRoaringBitmapContainer
{
    /// <summary>
    /// Actual data keeper.
    /// </summary>
    private readonly long[] chunks = new long[1024];

    /// <summary>
    /// The amount of 1-bits.
    /// </summary>
    public int Cardinality { get; private set; } = 0;

    public bool this[int i]
    {
        get
        {
            int chunkId = i / 64;
            int bit = BitwiseOperations.Mod2(i, 64);
            return BitwiseOperations.GetBit(chunks[chunkId], bit);
        }
        set
        {
            int chunkId = i / 64;
            int bit = BitwiseOperations.Mod2(i, 64);
            // Checking whether we are changing the value.
            bool prevValue = BitwiseOperations.GetBit(chunks[chunkId], bit);
            if (prevValue == false && value == true)
            {
                chunks[chunkId] = BitwiseOperations.SetBit(chunks[chunkId], bit, value);
                ++Cardinality;
            }
            else if (prevValue == true && value == false)
            {
                chunks[chunkId] = BitwiseOperations.SetBit(chunks[chunkId], bit, value);
                --Cardinality;
            }
        }
    }

    /// <summary>
    /// Performs logical AND with ArrayContainer instance.
    /// </summary>
    /// <returns> New ArrayContainer intance </returns>
    public ArrayContainer And(ArrayContainer other)
        => new ArrayContainer(other).And(this);

    /// <summary>
    /// Performs logical AND with BitmapContainerInstance.
    /// </summary>
    /// <param name="other"></param>
    /// <see cref="https://arxiv.org/pdf/1402.6407.pdf"/>
    /// <returns> Itself if result cardinality is > 4096, new ArrayContainer object otherwise </returns>
    public IRoaringBitmapContainer And(BitmapContainer other)
    {
        int c = 0;
        for (int i = 0; i < 1024; ++i)
        {
            chunks[i] &= other.chunks[i];
            c += BitwiseOperations.SparseBitcount(chunks[i]);
        }
        if (c > 4096)
        {
            Cardinality = c;
            return this;
        }
        else
        {
            ushort[] values = new ushort[c];
            int index = -1;
            for (int i = 0; i < 1024; ++i)
            {
                foreach (var pos in BitwiseOperations.TruePositions(chunks[i]))
                {
                    values[++index] = (ushort)(64 * i + pos);
                }
            }
            return new ArrayContainer(values);
        }
    }

    public IRoaringBitmapContainer And(IRoaringBitmapContainer other)
    {
        if (other is BitmapContainer) return And((BitmapContainer)other);
        return And((ArrayContainer)other);
    }

    /// <summary>
    /// Rebuilds BitmapContainer to ArrayContainer.
    /// </summary>
    public IRoaringBitmapContainer Rebuild(int newElem)
    {
        // Removing the 4097's element.
        this[newElem] = false;
        ushort[] values = new ushort[4096];
        int valuesIndex = 0;
        for (int i = 0; i < chunks.Length; ++i)
        {
            if (chunks[i] != 0)
            {
                foreach (var pos in BitwiseOperations.TruePositions(chunks[i]))
                {
                    values[valuesIndex++] = (ushort)(64 * i + pos);
                }
            }
        }
        return new ArrayContainer(values);
    }
}

/// <summary>
/// Fake full bitmap especially for the case of empty input.
/// </summary>
public class FakeFullBitmap : Bitmap
{
    public override void And(Bitmap other)
        => throw new NotSupportedException();

    public override bool Get(int i) => true;

    public override void Set(int i, bool value)
        => throw new NotSupportedException();
}

/// <summary>
/// Interface for bitmap containers (array and bitmap ones).
/// </summary>
public interface IRoaringBitmapContainer
{
    bool this[int i] { get; set; }
    int Cardinality { get; }
    /// <summary>
    /// Rebuilds one type of container to another. 
    /// </summary>
    /// <param name="newElemKey"> Element which caused the rebuilding. </param>
    IRoaringBitmapContainer Rebuild(int newElem);
    /// <summary>
    /// Performs logical and with other bitmap container.
    /// If it is possible, the no object is constructed (returns itself).
    /// </summary>
    IRoaringBitmapContainer And(IRoaringBitmapContainer other);
}

/// <summary>
/// Implementation of roating bitmap data structure.
/// </summary>
public class RoaringBitmap : Bitmap
{
    private int[] mostSignificantBits = new int[0];
    private IRoaringBitmapContainer[] containers = new IRoaringBitmapContainer[0];

    public override void And(Bitmap other)
    {
        RoaringBitmap roaringBitmap = other as RoaringBitmap;
        if (roaringBitmap != null)
        {
            // Performing logical AND for each chunk.
            for (int i = 0; i < containers.Length; i++)
            {
                // We look for similar chunk in other bitmap via GetContainerByKey.
                containers[i] = containers[i].And(roaringBitmap
                    .GetContainerByKey(mostSignificantBits[i]));
            }
        }
        else throw new InvalidOperationException(
            "Your bitmap isn't RoaringBitmap -- are you rewriting my code?");
    }

    public override bool Get(int i)
    {
        int sigBits = i / (1 << 16);
        // Looking for the particular container.
        int index = Array.BinarySearch(mostSignificantBits, sigBits);
        int val = BitwiseOperations.Mod2(i, 1 << 16);
        return index >= 0 && containers[index][val];
    }

    public override void Set(int i, bool value)
    {
        int key = i / (1 << 16);
        int index = Array.BinarySearch(mostSignificantBits, key);
        if (index >= 0)
        {
            int containerKey = BitwiseOperations.Mod2(i, 1 << 16);
            if (IsOverflowing(index, containerKey, value) ||
                IsUnderflowing(index, containerKey, value))
            {
                containers[index] = containers[index].Rebuild(containerKey);
            }
            else
            {
                containers[index][containerKey] = value;
            }
        }
        else
        {
            // If value is false, we don't have to bother creating a container.
            if (value)
            {
                Array.Resize(ref containers, containers.Length + 1);
                Array.Resize(ref mostSignificantBits, mostSignificantBits.Length + 1);
                for (int j = ~index + 1; j < containers.Length; ++j)
                {
                    containers[j] = containers[j - 1];
                    mostSignificantBits[j] = mostSignificantBits[j - 1];
                }
                mostSignificantBits[~index] = key;
                containers[~index] = new ArrayContainer();
                containers[~index][BitwiseOperations.Mod2(i, 1 << 16)] = true;
            }
        }
    }

    /// <summary>
    /// Indicates whether container at <paramref name="index"/> is going to overflow
    /// if we add <paramref name="containerKey"/>.
    /// </summary>
    private bool IsOverflowing(int index, int containerKey, bool value)
        => containers[index].Cardinality == 4096
                && containers[index][containerKey] == false && value == true;

    /// <summary>
    /// Indicates whether container at <paramref name="index"/> is going to underflow
    /// if we remove <paramref name="containerKey"/>.
    /// </summary>
    private bool IsUnderflowing(int index, int containerKey, bool value)
        => containers[index].Cardinality == 4097
                && containers[index][containerKey] == true && value == false;

    /// <summary>
    /// Looks for container with <paramref name="mostSignificantBits"/> most significant (16) bits.
    /// </summary>
    /// <param name="mostSignificantBits"></param>
    /// <returns> Container with elements with most significant bits <paramref name="mostSignificantBits"/>.
    /// Empty ArrayContainer, if such container wasn't found. </returns>
    private IRoaringBitmapContainer GetContainerByKey(int mostSignificantBits)
    {
        int index = Array.BinarySearch(this.mostSignificantBits, mostSignificantBits);
        if (index >= 0) return containers[index];
        return new ArrayContainer();
    }
}
#endregion

#region Invisible Join
/// <summary>
/// Holds information about the data base.
/// </summary>
public class DBModel
{
    private static readonly FilterProducer byteFilter =
        (op, other) => FilterBuilder.Get(op, other, byte.Parse);
    private static readonly FilterProducer shortFilter =
        (op, other) => FilterBuilder.Get(op, other, short.Parse);
    private static readonly FilterProducer intFilter =
        (op, other) => FilterBuilder.Get(op, other, int.Parse);
    private static readonly FilterProducer stringFilter =
        (op, other) => FilterBuilder.Get(op, other, x => x);

    /// <summary>
    /// Correlates dimensional table name with respective facts key column file.
    /// </summary>
    private static readonly Dictionary<string, string> dimToFactsCol =
        new Dictionary<string, string>
        {
            ["DimProduct"] = "FactResellerSales.ProductKey.csv",
            ["DimDate"] = "FactResellerSales.OrderDateKey.csv",
            ["DimReseller"] = "FactResellerSales.ResellerKey.csv",
            ["DimEmployee"] = "FactResellerSales.EmployeeKey.csv",
            ["DimCurrency"] = "FactResellerSales.CurrencyKey.csv",
            ["DimSalesTerritory"] = "FactResellerSales.SalesTerritoryKey.csv",
            ["DimPromotion"] = "FactResellerSales.PromotionKey.csv"
        };

    /// <summary>
    /// Default path to the folder with table files.
    /// </summary>
    private readonly string dataPath;
    /// <summary>
    /// Keeps a bitmap for each (at most) table.
    /// </summary>
    private Dictionary<string, Bitmap> tableBitmaps;
    /// <summary>
    /// Column processors by its names.
    /// </summary>
    private Dictionary<string, IColumnProcessor> columnProcessors;
    /// <summary>
    /// Cache of filtered lines of dimensional tables.
    /// </summary>
    private Dictionary<string, Dictionary<int, string[]>> dimTableLines;

    public DBModel(string dataPath)
    {
        this.dataPath = dataPath;
        dimTableLines = new Dictionary<string, Dictionary<int, string[]>>();
        tableBitmaps = new Dictionary<string, Bitmap>();
        columnProcessors = new Dictionary<string, IColumnProcessor>
        {
            ["FactResellerSales.ProductKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.ProductKey.csv"), intFilter, this),
            ["FactResellerSales.OrderDateKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.OrderDateKey.csv"), intFilter, this),
            ["FactResellerSales.ResellerKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.ResellerKey.csv"), intFilter, this),
            ["FactResellerSales.EmployeeKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.EmployeeKey.csv"), intFilter, this),
            ["FactResellerSales.PromotionKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.PromotionKey.csv"), intFilter, this),
            ["FactResellerSales.CurrencyKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.CurrencyKey.csv"), intFilter, this),
            ["FactResellerSales.SalesTerritoryKey"] = new PrimaryKeyFactsColumnProcessor(
                    GetTablePath("FactResellerSales.SalesTerritoryKey.csv"), intFilter, this),
            ["FactResellerSales.SalesOrderNumber"] = new FactsColumnProcessor(
                    GetTablePath("FactResellerSales.SalesOrderNumber.csv"), stringFilter, this),
            ["FactResellerSales.SalesOrderLineNumber"] = new FactsColumnProcessor(
                    GetTablePath("FactResellerSales.SalesOrderLineNumber.csv"), byteFilter, this),
            ["FactResellerSales.OrderQuantity"] = new FactsColumnProcessor(
                    GetTablePath("FactResellerSales.OrderQuantity.csv"), shortFilter, this),
            ["FactResellerSales.CarrierTrackingNumber"] = new FactsColumnProcessor(
                    GetTablePath("FactResellerSales.CarrierTrackingNumber.csv"), stringFilter, this),
            ["FactResellerSales.CustomerPONumber"] = new FactsColumnProcessor(
                    GetTablePath("FactResellerSales.CustomerPONumber.csv"), stringFilter, this),
        };
        InitializeDimProduct();
        InitializeDimReseller();
        InitializeDimCurrency();
        InitializeDimPromotion();
        InitializeDimSalesTerritory();
        InitializeDimEmployee();
        InitializeDimDate();
    }

    /// <summary>
    /// Returns column processor by the column full name.
    /// </summary>
    public IColumnProcessor GetColumn(string columnName) => columnProcessors[columnName];

    /// <summary>
    /// Phase 2 of Invisible Join algorighm: collecting bitmaps from each dimtable
    /// </summary>
    public void AccumulateResults()
    {
        if (tableBitmaps.Count != 0)
        {
            using (var enumerator = tableBitmaps.Keys.GetEnumerator())
            {
                enumerator.MoveNext();
                // We take the first one cause there can be a situation when FactResellerSales 
                // bitmap is absent (there were only DimTable queries).
                Bitmap facts = GetFactsBitmap(enumerator.Current);
                while (enumerator.MoveNext())
                {
                    facts.And(GetFactsBitmap(enumerator.Current));
                }
                tableBitmaps["FactResellerSales"] = facts;
            }
        }
        else
        {
            tableBitmaps["FactResellerSales"] = new FakeFullBitmap();
        }
    }

    /// <summary>
    /// Returns the facts table bitmap.
    /// </summary>
    /// <returns> Facts table bitmap. </returns>
    public Bitmap GetFactsBitmap() => tableBitmaps["FactResellerSales"];

    /// <summary>
    /// Returns the lines of dimensional table.
    /// </summary>
    public Dictionary<int, string[]> GetDimTableLines(string tableName)
    {
        if (!dimTableLines.ContainsKey(tableName))
        {
            dimTableLines[tableName] = new Dictionary<int, string[]>();
            if (tableBitmaps.ContainsKey(tableName))
            {
                // Reading only the filtered lines.
                var tableBitmap = tableBitmaps[tableName];
                TableReader.ForEachSplitedLine(GetTablePath(tableName + ".csv"), (i, parts) =>
                {
                    int primaryKey = int.Parse(parts[0]);
                    if (tableBitmap.Get(primaryKey))
                    {
                        dimTableLines[tableName][primaryKey] = parts;
                    }
                });
            }
            else
            {
                // Reading all lines.
                TableReader.ForEachSplitedLine(GetTablePath(tableName + ".csv"), (i, parts) =>
                {
                    dimTableLines[tableName][int.Parse(parts[0])] = parts;
                });
            }
        }
        return dimTableLines[tableName];
    }

    /// <summary>
    /// Combines the given bitmap with the existing bitmap of this table.
    /// </summary>
    public void PushBitmap(string tableName, Bitmap newFilterBitmap)
    {
        if (tableBitmaps.ContainsKey(tableName))
        {
            tableBitmaps[tableName].And(newFilterBitmap);
        }
        else
        {
            tableBitmaps[tableName] = newFilterBitmap;
        }
    }


    /// <summary>
    /// Combines path to data with table name to get correct path to the table.
    /// </summary>
    private string GetTablePath(string tableFileName)
        => Path.Combine(dataPath, tableFileName);

    /// <summary>
    /// Converts dim table bitmap to facts table bitmap.
    /// </summary>
    private Bitmap GetFactsBitmap(string dimTableName)
    {
        Bitmap dimBitmap = tableBitmaps[dimTableName];

        if (dimTableName == "FactResellerSales")
            return dimBitmap;

        string respectiveFactsTablePath = GetTablePath(dimToFactsCol[dimTableName]);
        RoaringBitmap bitmap = new RoaringBitmap();
        // Going through the respective facts column. If we see that dimBitmap contains
        // the value of the line, we add this line number to the constructing bitmap.
        TableReader.ForEachLine(respectiveFactsTablePath, (i, line) =>
        {
            if (dimBitmap.Get(int.Parse(line)))
            {
                bitmap.Set(i, true);
            }
        });
        return bitmap;
    }

    private void InitializeDimProduct()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.ProductKey"];
        string path = GetTablePath("DimProduct.csv");

        columnProcessors["DimProduct.ProductKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.ProductAlternateKey"] =
            new DimColumnProcessor(path, 1, stringFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.EnglishProductName"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.Color"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.SafetyStockLevel"] =
            new DimColumnProcessor(path, 4, shortFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.ReorderPoint"] =
            new DimColumnProcessor(path, 5, shortFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.SizeRange"] =
            new DimColumnProcessor(path, 6, stringFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.DaysToManufacture"] =
            new DimColumnProcessor(path, 7, intFilter, "DimProduct", this, factColumn);
        columnProcessors["DimProduct.StartDate"] =
            new DimColumnProcessor(path, 8, stringFilter, "DimProduct", this, factColumn);
    }

    private void InitializeDimReseller()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.ResellerKey"];
        string path = GetTablePath("DimReseller.csv");

        columnProcessors["DimReseller.ResellerKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.ResellerAlternateKey"] =
            new DimColumnProcessor(path, 1, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.Phone"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.BusinessType"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.ResellerName"] =
            new DimColumnProcessor(path, 4, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.NumberEmployees"] =
            new DimColumnProcessor(path, 5, intFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.OrderFrequency"] =
            new DimColumnProcessor(path, 6, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.ProductLine"] =
            new DimColumnProcessor(path, 7, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.AddressLine1"] =
           new DimColumnProcessor(path, 8, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.BankName"] =
           new DimColumnProcessor(path, 9, stringFilter, "DimReseller", this, factColumn);
        columnProcessors["DimReseller.YearOpened"] =
           new DimColumnProcessor(path, 10, intFilter, "DimReseller", this, factColumn);
    }

    private void InitializeDimCurrency()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.CurrencyKey"];
        string path = GetTablePath("DimCurrency.csv");

        columnProcessors["DimCurrency.CurrencyKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimCurrency", this, factColumn);
        columnProcessors["DimCurrency.CurrencyAlternateKey"] =
            new DimColumnProcessor(path, 1, stringFilter, "DimCurrency", this, factColumn);
        columnProcessors["DimCurrency.CurrencyName"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimCurrency", this, factColumn);
    }

    private void InitializeDimPromotion()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.PromotionKey"];
        string path = GetTablePath("DimPromotion.csv");

        columnProcessors["DimPromotion.PromotionKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.PromotionAlternateKey"] =
            new DimColumnProcessor(path, 1, intFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.EnglishPromotionName"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.EnglishPromotionType"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.EnglishPromotionCategory"] =
            new DimColumnProcessor(path, 4, stringFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.StartDate"] =
            new DimColumnProcessor(path, 5, stringFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.EndDate"] =
            new DimColumnProcessor(path, 6, stringFilter, "DimPromotion", this, factColumn);
        columnProcessors["DimPromotion.MinQty"] =
            new DimColumnProcessor(path, 7, intFilter, "DimPromotion", this, factColumn);
    }

    private void InitializeDimSalesTerritory()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.SalesTerritoryKey"];
        string path = GetTablePath("DimSalesTerritory.csv");

        columnProcessors["DimSalesTerritory.SalesTerritoryKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimSalesTerritory", this, factColumn);
        columnProcessors["DimSalesTerritory.SalesTerritoryAlternateKey"] =
            new DimColumnProcessor(path, 1, intFilter, "DimSalesTerritory", this, factColumn);
        columnProcessors["DimSalesTerritory.SalesTerritoryRegion"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimSalesTerritory", this, factColumn);
        columnProcessors["DimSalesTerritory.SalesTerritoryCountry"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimSalesTerritory", this, factColumn);
        columnProcessors["DimSalesTerritory.SalesTerritoryGroup"] =
            new DimColumnProcessor(path, 4, stringFilter, "DimSalesTerritory", this, factColumn);
    }

    private void InitializeDimEmployee()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.EmployeeKey"];
        string path = GetTablePath("DimEmployee.csv");

        columnProcessors["DimEmployee.EmployeeKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.FirstName"] =
            new DimColumnProcessor(path, 1, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.LastName"] =
            new DimColumnProcessor(path, 2, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.Title"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.BirthDate"] =
            new DimColumnProcessor(path, 4, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.LoginID"] =
            new DimColumnProcessor(path, 5, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.EmailAddress"] =
            new DimColumnProcessor(path, 6, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.Phone"] =
            new DimColumnProcessor(path, 7, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.MaritalStatus"] =
            new DimColumnProcessor(path, 8, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.Gender"] =
            new DimColumnProcessor(path, 9, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.PayFrequency"] =
            new DimColumnProcessor(path, 10, byteFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.VacationHours"] =
            new DimColumnProcessor(path, 11, shortFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.SickLeaveHours"] =
            new DimColumnProcessor(path, 12, shortFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.DepartmentName"] =
            new DimColumnProcessor(path, 13, stringFilter, "DimEmployee", this, factColumn);
        columnProcessors["DimEmployee.StartDate"] =
            new DimColumnProcessor(path, 14, stringFilter, "DimEmployee", this, factColumn);
    }

    private void InitializeDimDate()
    {
        var factColumn = (PrimaryKeyFactsColumnProcessor)columnProcessors["FactResellerSales.OrderDateKey"];
        string path = GetTablePath("DimDate.csv");

        columnProcessors["DimDate.DateKey"] =
            new DimColumnProcessor(path, 0, intFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.FullDateAlternateKey"] =
            new DimColumnProcessor(path, 1, stringFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.DayNumberOfWeek"] =
            new DimColumnProcessor(path, 2, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.EnglishDayNameOfWeek"] =
            new DimColumnProcessor(path, 3, stringFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.DayNumberOfMonth"] =
            new DimColumnProcessor(path, 4, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.DayNumberOfYear"] =
            new DimColumnProcessor(path, 5, shortFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.WeekNumberOfYear"] =
            new DimColumnProcessor(path, 6, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.EnglishMonthName"] =
            new DimColumnProcessor(path, 7, stringFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.MonthNumberOfYear"] =
            new DimColumnProcessor(path, 8, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.CalendarQuarter"] =
            new DimColumnProcessor(path, 9, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.CalendarYear"] =
            new DimColumnProcessor(path, 10, shortFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.CalendarSemester"] =
            new DimColumnProcessor(path, 11, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.FiscalQuarter"] =
            new DimColumnProcessor(path, 12, byteFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.FiscalYear"] =
            new DimColumnProcessor(path, 13, shortFilter, "DimDate", this, factColumn);
        columnProcessors["DimDate.FiscalSemester"] =
            new DimColumnProcessor(path, 14, byteFilter, "DimDate", this, factColumn);
    }
}

/// <summary>
/// Performs filter requests to the data base <see cref="DBModel"/>.
/// </summary>
public class DBRequester
{
    private readonly DBModel db;

    public DBRequester(string dataPath)
    {
        db = new DBModel(dataPath);
    }

    /// <summary>
    /// Applies a filter given as a string.
    /// </summary>
    public void Query(string query)
    {
        string columnName = query.Substring(0, query.IndexOf(' '));
        query = query.Substring(query.IndexOf(' ') + 1);
        string operation = query.Substring(0, query.IndexOf(' '));
        string value = query.Substring(query.IndexOf(' ') + 1);

        // In this case we have a string.
        if (value[0] == '\'')
        {
            // Deleting the first and last '
            value = value.Substring(1, value.Length - 2);
        }

        db.GetColumn(columnName).Filter(operation, value);
    }

    /// <summary>
    /// Accumulates all bitmaps and returns list of lines, where values of 
    /// <paramref name="outputCols"/> are delimeted by '|'.
    /// </summary>
    public List<string> GetResults(string[] outputCols)
    {
        db.AccumulateResults();

        List<List<string>> results = new List<List<string>>();
        // Third phase of invisible join: 
        // getting values from dimensional tables using facts table bitmap.
        foreach (var col in outputCols)
        {
            results.Add(db.GetColumn(col).GetValues());
        }
        List<string> result = new List<string>();
        for (int i = 0; i < results[0].Count; ++i)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var list in results)
            {
                sb.Append(list[i]).Append('|');
            }
            sb.Remove(sb.Length - 1, 1);
            result.Add(sb.ToString());
        }
        return result;
    }
}

/// <summary>
/// Processes particular columns of dimensional tables.
/// </summary>
public class DimColumnProcessor : IColumnProcessor
{
    /// <summary>
    /// Path to dimensional table.
    /// </summary>
    private readonly string path;
    /// <summary>
    /// Column number, which this instance represent.
    /// </summary>
    private readonly int columnNum;
    /// <summary>
    /// Facts table, which keeps primary keys of this dimensional table.
    /// </summary>
    private readonly PrimaryKeyFactsColumnProcessor primaryKeysFactsColumn;
    /// <summary>
    /// Filter producer especially for this column (depends on content type).
    /// </summary>
    private readonly FilterProducer filterProducer;
    /// <summary>
    /// DB main object.
    /// </summary>
    private readonly DBModel db;
    /// <summary>
    /// Name of the dimensional table.
    /// </summary>
    private readonly string tableName;

    public DimColumnProcessor(string path, int columnNum,
        FilterProducer filterProducer, string tableName,
        DBModel db, PrimaryKeyFactsColumnProcessor factsColumn)
    {
        this.path = path;
        this.columnNum = columnNum;
        primaryKeysFactsColumn = factsColumn;
        this.db = db;
        this.filterProducer = filterProducer;
        this.tableName = tableName;
    }

    /// <summary>
    /// Filters the table with given operation and rhs value.
    /// </summary>
    public void Filter(string operation, string otherValue)
        => Filter(filterProducer(operation, otherValue));

    /// <summary>
    /// Returns values of the column after all filtrations.
    /// </summary>
    public List<string> GetValues()
    {
        // We use respective facts column to extract primary keys.
        var keys = primaryKeysFactsColumn.GetKeys();
        // Then we get [cached] table lines as dictionary.
        var lines = db.GetDimTableLines(tableName);
        var result = new List<string>();
        // For each key we get the respective value.
        foreach (var key in keys)
        {
            result.Add(lines[key][columnNum]);
        }
        return result;
    }


    /// <summary>
    /// Filters the table using given predicate.
    /// </summary>
    private void Filter(Predicate<string> filter)
    {
        RoaringBitmap dimBitmap = new RoaringBitmap();
        // Constructing bitmap based on table.
        TableReader.ForEachSplitedLine(path, (i, parts) =>
        {
            if (filter(parts[columnNum]))
            {
                    // Parts[0] is primary key.
                    dimBitmap.Set(int.Parse(parts[0]), true);
            }
        });
        // Pushing bitmap to db.
        db.PushBitmap(tableName, dimBitmap);
    }
}

/// <summary>
/// Processes particular columns of the facts table.
/// </summary>
public class FactsColumnProcessor : IColumnProcessor
{
    private const string tableName = "FactResellerSales";

    /// <summary>
    /// Path to facts table column.
    /// </summary>
    protected string Path { get; }
    /// <summary>
    /// DB main object.
    /// </summary>
    protected DBModel Db { get; }
    /// <summary>
    /// Filter producer especially for this column (depends on content type).
    /// </summary>
    private readonly FilterProducer filterProducer;

    public FactsColumnProcessor(string path, FilterProducer filterProducer, DBModel db)
    {
        Path = path;
        this.filterProducer = filterProducer;
        Db = db;
    }

    /// <summary>
    /// Filters table using given comparison operation and rhs value.
    /// </summary>
    public void Filter(string operation, string otherValue)
        => Filter(filterProducer(operation, otherValue));

    /// <summary>
    /// Returns values of the column after all filtrations.
    /// </summary>
    public List<string> GetValues()
    {
        Bitmap factsTableMask = Db.GetFactsBitmap();
        // The only case when this method will be called more than once is when
        // we specified the column as an output one more than one time (and it's kinda ridiculous).
        // Thats why I don't cache this one.
        var result = new List<string>();
        TableReader.ForEachLine(Path, (i, line) =>
        {
            if (factsTableMask.Get(i))
            {
                result.Add(line);
            }
        });

        return result;
    }


    /// <summary>
    /// Filters the table using given predicate.
    /// </summary>
    private void Filter(Predicate<string> filter)
    {
        var bitmap = new RoaringBitmap();
        TableReader.ForEachLine(Path, (i, line) =>
        {
            if (filter(line))
            {
                bitmap.Set(i, true);
            }
        });
        Db.PushBitmap(tableName, bitmap);
    }
}

/// <summary>
/// Represents db column. Filters values by predicate and returns all values after number of filtrations.
/// </summary>
public interface IColumnProcessor
{
    void Filter(string operation, string otherValue);

    List<string> GetValues();
}

/// <summary>
/// Extension for primary keys column. Caches the keys.
/// </summary>
public class PrimaryKeyFactsColumnProcessor : FactsColumnProcessor
{
    private List<int> primaryKeysCache = null;

    public PrimaryKeyFactsColumnProcessor(string path, FilterProducer filterProducer,
        DBModel db) : base(path, filterProducer, db)
    { }

    /// <summary>
    /// Returns values of this column based on the facts bitmap.
    /// </summary>
    public List<int> GetKeys()
    {
        if (primaryKeysCache == null)
        {
            primaryKeysCache = new List<int>();
            Bitmap bitmap = Db.GetFactsBitmap();
            TableReader.ForEachLine(Path, (i, line) =>
            {
                if (bitmap.Get(i))
                {
                    primaryKeysCache.Add(int.Parse(line));
                }
            });
        }
        return primaryKeysCache;
    }
}
#endregion

#region Utils
/// <summary>
/// Keeps element in a sorted array of unique 16-bit integer (ushort) values.
/// </summary>
public class ArraySet
{
    /// <summary>
    /// Max size of array.
    /// It is not an additional memory, but a compile time constant.
    /// </summary>
    private const int maxSize = 4096;
    private ushort[] array;

    public ArraySet() => array = new ushort[0];

    /// <summary>
    /// Creates a set based on given ushort array.
    /// ATTENTION: no deep copying is performed!
    /// Since we have absolutely no way to change the inner array 
    /// (each insertion and deletion is made with reallocating) it is safe.
    /// </summary>
    public ArraySet(ushort[] other) => array = other;

    /// <summary>
    /// Creates a set based on other set.
    /// </summary>
    /// <see cref="ArraySet(ushort[])"/>
    public ArraySet(ArraySet other) : this(other.array) { }

    public int Cardinality => array.Length;

    /// <summary>
    /// We need access to the elements in couple of places.
    /// </summary>
    public ushort this[int i] => array[i];

    public bool Contains(ushort value)
        => Array.BinarySearch(array, value) >= 0;

    public void Insert(ushort value)
    {
        int index = Array.BinarySearch(array, value);
        if (index < 0)
        {
            if (Cardinality == maxSize)
                throw new InvalidOperationException("Cannot add a new element to full array set!");
            Array.Resize(ref array, array.Length + 1);
            for (int i = array.Length - 1; i > ~index; --i)
            {
                array[i] = array[i - 1];
            }
            array[~index] = value;
        }
    }

    public void Intersect(ArraySet other)
    {
        // Empty set intersection is empty set.
        if (array.Length == 0 || other.array.Length == 0)
        {
            array = new ushort[0];
            return;
        }
        // i to go through inner array, j to go throw other array, k for result array.
        int i = 0, j = 0, k = 0;
        // Merging two sorted arrays.
        // The condition for exiting loop is end of one of the arrays.
        while (i != array.Length && j != other.array.Length)
        {
            if (array[i] < other.array[j]) ++i;
            else if (array[i] > other.array[j]) ++j;
            else
            {
                // We can use the inner array as an array for result, cause the result
                // of this whole operation must be subset of array.
                array[k++] = array[i];
                ++i; ++j;
            }
        }
        // If we've checked all the elements in the inner array, we need to check the rest of the other array.
        if (i == array.Length)
        {
            ushort last = array[array.Length - 1];
            for (; j < other.array.Length; ++j)
            {
                if (last == other.array[j]) array[k++] = last;
                else if (last < other.array[j]) break;
            }
        }
        // If we've checked all the elements in the other array, we need to check the rest of the inner array.
        else if (j == array.Length)
        {
            ushort last = other.array[other.array.Length - 1];
            for (; i < array.Length; ++i)
            {
                if (array[i] == last) array[k++] = last;
                else if (array[i] > last) break;
            }
        }
        // At that point k is new array size.
        if (k != array.Length)
        {
            Array.Resize(ref array, k);
        }
    }

    /// <summary>
    /// Removes element by value (if exists).
    /// </summary>
    public void Remove(ushort value)
    {
        int index = Array.BinarySearch(array, value);
        if (index >= 0) RemoveAt(index);
    }

    /// <summary>
    /// Removes element at particular index.
    /// </summary>
    public void RemoveAt(int index)
    {
        for (int i = index; i < array.Length - 1; ++i)
        {
            array[i] = array[i + 1];
        }
        Array.Resize(ref array, array.Length - 1);
    }
}

/// <summary>
/// A bit of stuff.
/// </summary>
public static class BitwiseOperations
{
    /// <summary>
    /// Returns value of certain bit of integer value.
    /// </summary>
    public static bool GetBit(long value, int bitPos)
        => (((long)1 << bitPos) & value) != 0;

    public static int Div2(int value, int powerOf2)
        => (value + ((value >> 31) & ((1 << powerOf2) + ~0))) >> powerOf2;

    /// <summary>
    /// Returns remained by given powerOf2.
    /// ATTENTION: powerOf2 must be actually a power of 2.
    /// </summary>
    public static int Mod2(int value, int powerOf2) => value & (powerOf2 - 1);

    /// <summary>
    /// Sets a particular bit of long value to the given bool value.
    /// </summary>
    public static long SetBit(long value, int bitPos, bool newBitValue)
    {
        long mask = (long)1 << bitPos;
        return newBitValue ? (value | mask) : (value & ~mask);
    }

    /// <summary>
    /// Popcnt for losers.
    /// </summary>
    public static int SparseBitcount(long n)
    {
        int count = 0;
        while (n != 0)
        {
            ++count;
            n &= (n - 1);
        }
        return count;
    }

    /// <summary>
    /// Finds all occurences of 1 in binary integer representation.
    /// </summary>
    /// <see cref="https://arxiv.org/pdf/1402.6407.pdf"/> 
    /// <returns> Positions ([0..63]) where 1 occures. </returns>
    public static List<int> TruePositions(long value)
    {
        List<int> res = new List<int>();
        long t;
        while (value != 0)
        {
            t = value & (-value);
            res.Add(SparseBitcount(t - 1));
            value &= value - 1;
        }
        return res;
    }
}

/// <summary>
/// Builds a string predicate based on operator, rhs value and type converter.
/// </summary>
public static class FilterBuilder
{
    public static Predicate<string> Get<T>(
        string operation, string other, Converter<string, T> converter)
        where T : IComparable<T>
    {
        switch (operation)
        {
            case "<":
                return x => converter(x).CompareTo(converter(other)) < 0;
            case "<=":
                return x => converter(x).CompareTo(converter(other)) <= 0;
            case ">":
                return x => converter(x).CompareTo(converter(other)) > 0;
            case ">=":
                return x => converter(x).CompareTo(converter(other)) >= 0;
            case "=":
                return x => converter(x).CompareTo(converter(other)) == 0;
            case "<>":
                return x => converter(x).CompareTo(converter(other)) != 0;
            default:
                throw new ArgumentException("Wrong operation: " + operation);
        }
    }
}

/// <summary>
/// Takes operation and rhs value and returns predicate. Declared it to keep it simple.
/// </summary>
public delegate Predicate<string> FilterProducer(string operation, string otherValue);

/// <summary>
/// Reads table line-by-line.
/// </summary>
public static class TableReader
{
    /// <summary>
    /// Performs an action for each line of the table. 
    /// </summary>
    /// <param name="path"> Path to the table. </param>
    /// <param name="action"> Action which is performed for each line. First argument is line num (from 0), second is the actual line. </param>
    public static void ForEachLine(string path, Action<int, string> action)
    {
        using (StreamReader sr = new StreamReader(path))
        {
            int i = 0;
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                action(i, line);
                ++i;
            }
        }
    }

    /// <summary>
    /// Performs an action for each line of the table.
    /// </summary>
    /// <param name="path"> Path to the table. </param>
    /// <param name="action"> Action which is performed for each line. First argument is line num (from 0), second is the actual line. </param>
    public static void ForEachSplitedLine(string path, Action<int, string[]> action)
    {
        using (StreamReader sr = new StreamReader(path))
        {
            int i = 0;
            string[] parts;
            while (!sr.EndOfStream)
            {
                parts = sr.ReadLine().Split('|');
                action(i, parts);
                ++i;
            }
        }
    }
}

/// <summary>
/// For tests.
/// </summary>
public class Tester
{
    private readonly string testsPath;
    private readonly string testName;
    private readonly string answersPath;
    private readonly string answerName;
    private readonly string myAnswersPath;
    private readonly string myAnswerName;

    public Tester(string testsPath = "input", string testName = "test",
        string answersPath = "output", string answerName = "answer",
        string myAnswersPath = "myOutput", string myAnswerName = "myAnswer")
    {
        this.testsPath = testsPath;
        this.testName = testName;
        this.answersPath = answersPath;
        this.answerName = answerName;
        this.myAnswersPath = myAnswersPath;
        this.myAnswerName = myAnswerName;
    }

    public void Test(int from, int to)
    {
        for (int i = from; i <= 5; ++i) Test(i);
        Console.ResetColor();
    }

    public void Test(int num)
    {
        string testPath = Path.Combine(testsPath, $"{testName}{num}.txt");
        string answerPath = Path.Combine(answersPath, $"{answerName}{num}.txt");
        string myAnswerPath = Path.Combine(myAnswersPath, $"{myAnswerName}{num}.txt");

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Starting test {testPath}...");
        Console.ResetColor();

        Program.Run("data", testPath, myAnswerPath);

        using (StreamReader myAnswer = new StreamReader(myAnswerPath))
        using (StreamReader answer = new StreamReader(answerPath))
        {
            string myLine = null, answerLine = null;
            int lineNum = 1;
            while (!(myAnswer.EndOfStream && answer.EndOfStream))
            {
                myLine = myAnswer.ReadLine();
                answerLine = answer.ReadLine();
                Console.ResetColor();
                Console.Write($"Line {lineNum}: ");
                if ((myLine == null) ^ (answerLine == null))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[-] Line number doesn't match!");
                    break;
                }
                else if (myLine != answerLine)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[-] Answers don't match at line {lineNum}: " +
                        $"expected {answerLine} but got {myLine}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] Match");
                }

                ++lineNum;
            }
        }
        Console.WriteLine("End of test.");
    }

    public void TestTime(int from, int to)
    {
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        for (int i = from; i <= to; ++i)
        {
            string testPath = Path.Combine(testsPath, $"test{i}.txt");
            string myAnswerPath = Path.Combine(myAnswersPath, $"myAnswer{i}.txt");
            Program.Run("data", testPath, myAnswerPath);
        }
        stopwatch.Stop();
        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }
}
#endregion

