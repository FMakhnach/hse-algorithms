using RoaringBitmap_InvisibleJoin.Bitmaps;
using RoaringBitmap_InvisibleJoin.InvisibleJoin.ColumnProcess;
using RoaringBitmap_InvisibleJoin.Utils;
using System.Collections.Generic;
using System.IO;
namespace RoaringBitmap_InvisibleJoin.InvisibleJoin
{
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
}
