﻿// -----------------------------------------------------------------------
// <copyright file="APITest.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Importer;
    using Models;
    using Models.Core;
    using Models.Graph;
    using Models.Soils;
    using NUnit.Framework;

    /// <summary> 
    /// This is a test class for SystemComponentTest and is intended
    /// to contain all SystemComponentTest Unit Tests
    /// </summary>
    [TestFixture]
    public class APITest
    {
        /// <summary>
        /// A simulations instance
        /// </summary>
        private Simulations simulations;
        
        /// <summary>
        /// A simulation instance
        /// </summary>
        private Simulation simulation;

        /// <summary>
        /// Start up code for all tests.
        /// </summary>
        [SetUp]
        public void Initialise()
        {
            FileStream oldfile = new FileStream("Continuous_Wheat.apsim", FileMode.Create);
            oldfile.Write(UnitTests.Properties.Resources.Continuous_Wheat, 0, UnitTests.Properties.Resources.Continuous_Wheat.Length);
            oldfile.Close();
            
            FileStream f = new FileStream("Test.apsimx", FileMode.Create);
            f.Write(UnitTests.Properties.Resources.TestFile, 0, UnitTests.Properties.Resources.TestFile.Length);
            f.Close();
            FileStream w = new FileStream("Goondiwindi.met", FileMode.Create);
            w.Write(UnitTests.Properties.Resources.Goondiwindi, 0, UnitTests.Properties.Resources.Goondiwindi.Length);
            w.Close();
            this.simulations = Simulations.Read("Test.apsimx");
            
            string sqliteSourceFileName = this.FindSqlite3DLL();

            string sqliteFileName = Path.Combine(Directory.GetCurrentDirectory(), "sqlite3.dll");
            if (!File.Exists(sqliteFileName))
            {
                File.Copy(sqliteSourceFileName, sqliteFileName);
            }

            this.simulation = this.simulations.Children[0] as Simulation;
            this.simulation.StartRun();
        }

        /// <summary>
        /// Clean up code for all tests.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            this.simulation.CleanupRun();
            File.Delete("Test.apsimx");
            File.Delete("Goondiwindi.met");
        }

        /// <summary>
        /// A test for the FullPath method
        /// </summary>
        [Test]
        public void FullPathTest()
        {
            Zone zone2 = this.simulation.Children[4] as Zone;
            Graph graph = zone2.Children[0] as Graph;
            Soil soil = zone2.Children[1] as Soil;
            
            Assert.AreEqual(Apsim.FullPath(this.simulation), ".Simulations.Test");
            Assert.AreEqual(Apsim.FullPath(zone2), ".Simulations.Test.Field2");
            Assert.AreEqual(Apsim.FullPath(soil.Water), ".Simulations.Test.Field2.Soil.Water");
        }

        /// <summary>
        /// A test to ensure that 'Models' property is valid.
        /// </summary>
        [Test]
        public void ModelsTest()
        {
            Assert.AreEqual(this.simulations.Children.Count, 3);
            Assert.AreEqual(this.simulations.Children[0].Name, "Test");

            Assert.AreEqual(this.simulation.Children.Count, 5);
            Assert.AreEqual(this.simulation.Children[0].Name, "WeatherFile");
            Assert.AreEqual(this.simulation.Children[1].Name, "Clock");
            Assert.AreEqual(this.simulation.Children[2].Name, "Summary");
            Assert.AreEqual(this.simulation.Children[3].Name, "Field1");
            Assert.AreEqual(this.simulation.Children[4].Name, "Field2");

            Zone zone = this.simulation.Children[3] as Zone;
            Assert.AreEqual(zone.Children.Count, 1);
            Assert.AreEqual(zone.Children[0].Name, "Field1Report");
        }
        
        /// <summary>
        /// A test to ensure that Parent of type method works.
        /// </summary>
        [Test]
        public void ParentTest()
        {
            Zone zone2 = this.simulation.Children[4] as Zone;
            Graph graph = zone2.Children[0] as Graph;
            
            Assert.NotNull(Apsim.Parent(this.simulation, typeof(Simulations)));
            Assert.AreEqual(Apsim.Parent(graph, typeof(Simulations)).Name, "Simulations");
            Assert.AreEqual(Apsim.Parent(graph, typeof(Simulation)).Name, "Test");
            Assert.AreEqual(Apsim.Parent(graph, typeof(Zone)).Name, "Field2");
        }

        /// <summary>
        /// FindAll method tests.
        /// </summary>
        [Test]
        public void FindAllTest()
        {
            Zone zone2 = this.simulation.Children[4] as Zone;
            Graph graph = zone2.Children[0] as Graph;
            Soil soil = zone2.Children[1] as Soil;

            // Test the models that are in scope of zone2.graph
            List<IModel> inScopeForGraph = Apsim.FindAll(graph);
            Assert.AreEqual(inScopeForGraph.Count, 10);
            Assert.AreEqual(inScopeForGraph[0].Name, "Graph1");
            Assert.AreEqual(inScopeForGraph[1].Name, "Soil");
            Assert.AreEqual(inScopeForGraph[2].Name, "SurfaceOrganicMatter");
            Assert.AreEqual(inScopeForGraph[3].Name, "Field2SubZone");
            Assert.AreEqual(inScopeForGraph[4].Name, "Field2");
            Assert.AreEqual(inScopeForGraph[5].Name, "WeatherFile");
            Assert.AreEqual(inScopeForGraph[6].Name, "Clock");
            Assert.AreEqual(inScopeForGraph[7].Name, "Summary");
            Assert.AreEqual(inScopeForGraph[8].Name, "Field1");
            Assert.AreEqual(inScopeForGraph[9].Name, "Test");
            
            List<IModel> zones = Apsim.FindAll(graph, typeof(Zone));
            Assert.AreEqual(zones.Count, 4);
            Assert.AreEqual(zones[0].Name, "Field2SubZone");
            Assert.AreEqual(zones[1].Name, "Field2");
            Assert.AreEqual(zones[2].Name, "Field1");
        }

        /// <summary>
        /// Scoping rule tests. Find method
        /// </summary>
        [Test]
        public void FindTest()
        {
            Zone field1 = this.simulation.Children[3] as Zone;

            // Make sure we can get a link to a local model from Field1
            Assert.AreEqual(Apsim.Find(field1, "Field1Report").Name, "Field1Report");
            Assert.AreEqual(Apsim.Find(field1, typeof(Models.Report)).Name, "Field1Report");

            // Make sure we can get a link to a model in top level zone from Field1
            Assert.AreEqual(Apsim.Find(field1, "WeatherFile").Name, "WeatherFile");
            Assert.AreEqual(Apsim.Find(field1, typeof(Models.WeatherFile)).Name, "WeatherFile");

            // Make sure we can't get a link to a model in Field2 from Field1
            Assert.IsNull(Apsim.Find(field1, "Graph"));
            Assert.IsNull(Apsim.Find(field1, typeof(Models.Graph.Graph)));

            // Make sure we can get a link to a model in a child field.
            Zone field2 = this.simulation.Children[4] as Zone;
            Assert.IsNotNull(Apsim.Find(field2, "Field2SubZoneReport"));
            Assert.IsNotNull(Apsim.Find(field2, typeof(Models.Report)));

            // Make sure we can get a link from a child, child zone to the top level zone.
            Zone field2SubZone = field2.Children[3] as Zone;
            Assert.AreEqual(Apsim.Find(field2SubZone, "WeatherFile").Name, "WeatherFile");
            Assert.AreEqual(Apsim.Find(field2SubZone, typeof(Models.WeatherFile)).Name, "WeatherFile");
        }

        /// <summary>
        /// Tests for the get method
        /// </summary>
        [Test]
        public void GetTest()
        {
            Zone zone2 = this.simulation.Children[4] as Zone;
            Graph graph = zone2.Children[0] as Graph;
            Soil soil = zone2.Children[1] as Soil;

            Zone field1 = this.simulation.Children[3] as Zone;
            
            // Make sure we can get a link to a local model from Field1
            Assert.AreEqual((field1.Get("Field1Report") as IModel).Name, "Field1Report");
            
            // Make sure we can get a variable from a local model.
            Assert.AreEqual(field1.Get("Field1Report.Name"), "Field1Report");

            // Make sure we can get a variable from a local model using a full path.
            Assert.AreEqual((field1.Get(".Simulations.Test.Field1.Field1Report") as IModel).Name, "Field1Report");
            Assert.AreEqual(field1.Get(".Simulations.Test.Field1.Field1Report.Name"), "Field1Report");

            // Make sure we get a null when trying to link to a top level model from Field1
            Assert.IsNull(field1.Get("WeatherFile"));

            // Make sure we can get a top level model from Field1 using a full path.
            Assert.AreEqual(Utility.Reflection.Name(field1.Get(".Simulations.Test.WeatherFile")), "WeatherFile");

            // Make sure we can get a model in Field2 from Field1 using a full path.
            Assert.AreEqual(Utility.Reflection.Name(field1.Get(".Simulations.Test.Field2.Graph1")), "Graph1");

            // Make sure we can get a property from a model in Field2 from Field1 using a full path.
            Assert.AreEqual(field1.Get(".Simulations.Test.Field2.Graph1.Name"), "Graph1");

            // Make sure we can get a property from a model in Field2/Field2SubZone from Field1 using a full path.
            Assert.AreEqual(field1.Get(".Simulations.Test.Field2.Field2SubZone.Field2SubZoneReport.Name"), "Field2SubZoneReport");
            
            // Test the in scope capability of get.
            Assert.AreEqual(zone2.Get("[Graph1].Name"), "Graph1");
            Assert.AreEqual(zone2.Get("[Soil].Water.Name"), "Water");
        }
        
        /// <summary>
        /// Tests for the set method
        /// </summary>
        [Test]
        public void SetTest()
        {
            WeatherFile weather = this.simulation.Children[0] as WeatherFile;
            Assert.AreEqual(this.simulation.Get("[WeatherFile].Rain"), 0.0);
            this.simulation.Set("[WeatherFile].Rain", 111.0);
            Assert.AreEqual(this.simulation.Get("[WeatherFile].Rain"), 111.0);
        }

        /// <summary>
        /// Tests for Children
        /// </summary>
        [Test]
        public void ChildrenTest()
        {
            List<IModel> allChildren = Apsim.Children(this.simulation, typeof(Zone));
            Assert.AreEqual(allChildren.Count, 2);
        }
        
        /// <summary>
        /// Tests for Child method
        /// </summary>
        [Test]
        public void ChildTest()
        {
            IModel clock = Apsim.Child(simulation, typeof(Clock));
            Assert.NotNull(clock);
            clock = Apsim.Child(simulation, "Clock");
            Assert.NotNull(clock);
        }        
        
        /// <summary>
        /// Tests for the various recursive Children methods
        /// </summary>
        [Test]
        public void ChildrenRecursivelyTest()
        {
            List<IModel> allChildren = Apsim.ChildrenRecursively(simulation);
            Assert.AreEqual(allChildren.Count, 18);

            List<IModel> childZones = Apsim.ChildrenRecursively(simulation, typeof(Zone));
            Assert.AreEqual(childZones.Count, 3);
        }

        /// <summary>
        /// Tests for siblings method
        /// </summary>
        [Test]
        public void SiblingsTest()
        {
            IModel clock = Apsim.Child(simulation, typeof(Clock));
            List<IModel> allSiblings = Apsim.Siblings(clock);
            Assert.AreEqual(allSiblings.Count, 4);
        }
          
        /// <summary>
        /// Tests for the weather model 
        /// </summary>
        [Test]
        public void WeatherSummary()
        {
            Assert.AreEqual(this.simulation.Children[0].Name, "WeatherFile");

            foreach (Model model in this.simulation.Children) 
            {
                if (model.GetType() == typeof(WeatherFile)) 
                {
                    WeatherFile wtr = model as WeatherFile;
                    Assert.AreNotEqual(wtr.GetAllData(), null, "Weather file stream");
                    Assert.AreEqual(wtr.Amp, 15.96, "TAMP");
                    Assert.AreEqual(wtr.Tav, 19.86, "TAV");
                    
                    // for the first day
                    Assert.AreEqual(wtr.DayLength, 14.7821247010713, 0.0001, "Day length");
                    
                    // check yearly and monthly aggregations
                    double[] yrtotal, avmonth;
                    wtr.YearlyRainfall(out yrtotal, out avmonth);
                    Assert.AreEqual(yrtotal[0], 420, 0.001, "Yearly 1 totals");
                    Assert.AreEqual(yrtotal[49], 586.1, 0.001, "Yearly 50 totals");
                    Assert.AreEqual(avmonth[0], 79.7, 0.001, "LTAV1 Monthly");
                    Assert.AreEqual(avmonth[11], 62.8259, 0.001, "LTAV12 Monthly");
                }
            }
        }
        
        /// <summary>
        /// Tests for the importer
        /// </summary>
        [Test]
        public void ImportOldAPSIM()
        {
            // test the importing of an example simulation from APSIM 7.6
            APSIMImporter importer = new APSIMImporter();
            importer.ProcessFile("Continuous_Wheat.apsim");

            Simulations testrunSimulations = Simulations.Read("Continuous_Wheat.apsimx");
           
            Assert.IsNotNull(Apsim.Find(testrunSimulations, "wheat"));
            Assert.IsNotNull(Apsim.Find(testrunSimulations, "clock"));
            Assert.IsNotNull(Apsim.Find(testrunSimulations, "SoilNitrogen"));
            Assert.IsNotNull(Apsim.Find(testrunSimulations, "SoilWater"));
        }

        /// <summary>
        /// Find an return the database file name.
        /// </summary>
        /// <returns>The filename</returns>
        private string FindSqlite3DLL()
        {
            string directory = Directory.GetCurrentDirectory();
            while (directory != null) 
            {
                string[] directories = Directory.GetDirectories(directory, "Bin");
                if (directories.Length == 1) 
                {
                    string[] files = Directory.GetFiles(directories[0], "sqlite3.dll");
                    if (files.Length == 1)
                    {
                        return files[0];
                    }
                }
                
                directory = Path.GetDirectoryName(directory); // parent directory
            }
            
            throw new Exception("Cannot find apsimx bin directory");
        }        
    }
}