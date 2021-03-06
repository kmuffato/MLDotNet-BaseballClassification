﻿using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;

namespace MLDotNet_BaseballClassification
{
    class Program
    {
        // Set up path locations
        private static string appFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string _trainDataPath => Path.Combine(appFolder, "Data", "BaseballHOFTraining.csv");
        private static string _testDataPath => Path.Combine(appFolder, "Data", "BaseballHOFTest.csv");
        private static string _fullDataPath => Path.Combine(appFolder, "Data", "BaseballHOFTrainingFull.csv");
        private static string _performanceMetricsTrainTestModels => Path.Combine(appFolder, @"ModelPerformanceMetrics", "PerformanceMetricsTrainTestModels.csv");

        // Thread-safe ML Context
        private static MLContext _mlContext;
        // Set seed to static value for re-producable model results (or DateTime for pseudo-random)
        private static int seed = 100;

        private static string _labelColunmn = "OnHallOfFameBallot";

        // Configuration Arrays

        // List of feature columns used for training
        // Useage: Comment out (or uncomment) feature names in order to explicitly select features for model training
        private static string[] featureColumns = new string[] {
            "YearsPlayed", "AB", "R", "H", "Doubles", "Triples", "HR", "RBI", "SB",
            "BattingAverage", "SluggingPct", "AllStarAppearances", "MVPs", "TripleCrowns", "GoldGloves",
            "MajorLeaguePlayerOfTheYearAwards", "TB", "TotalPlayerAwards"
        };
        private static string featureColumnsStringArray = String.Join(",", featureColumns);

        // List of supervised learning labels
        // Useage: At least one must be left
        private static string[] labelColumns = new string[] { "OnHallOfFameBallot", "InductedToHallOfFame" };

        // List of algorithms that support probability output
        // Useage: Comment out (or uncomment) algorithm names to report model explainability
        private static string[] algorithmsForModelExplainability = new string[] {
                "FieldAwareFactorization",
                "GeneralizedAdditiveModels", "LogisticRegression",
                "FastTree", "LightGbm",
                "StochasticGradientDescentCalibrated"
        };

        static void Main(string[] args)
        {
            // Start stopwatch to time model job
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Console.Title = "Baseball Predictions - Training Model Job";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Starting Baseball Predictions - Training Model Job");
            Console.WriteLine("Using ML.NET - Version 1.5.1");
            Console.WriteLine();
            Console.ResetColor();
            Console.WriteLine("This training job will build a series of models that will predict both:");
            Console.WriteLine("1) Whether a baseball batter would make it on the HOF Ballot (OnHallOfFameBallot)");
            Console.WriteLine("2) Whether a baseball batter would be inducted to the HOF (InductedToHallOfFame).");
            Console.WriteLine("Based on an MLB batter's summarized career batting statistics.\n");
            Console.WriteLine("Note: The goal is to build a 'good enough' set of models & showcase the ML.NET framework.");
            Console.WriteLine("Note: For better models advanced historical scaling and features should be performed.");
            Console.WriteLine();

            #region Step 1) ML.NET Setup & Load Data

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("###############################");
            Console.WriteLine("Step 1: Load Data from files...");
            Console.WriteLine("###############################\n");
            Console.ResetColor();

            // Set the seed explicitly for reproducability (models will be built with consistent results)
            _mlContext = new MLContext(seed: seed);

            // Read the training/validation data from a text file
            //var dataTrainBatters = File.ReadAllLines(_trainDataPath)
            //    .Skip(1) // Skip the CSV Header
            //    .Select(v => MLBBaseballBatter.FromCsv(v))
            //    .AsQueryable() // Allows for Dyanmic Linq
            //    .Select("new (" + featureColumnsStringArray + ")")
            //    .ToDynamicList();

            var dataTrain = _mlContext.Data.LoadFromTextFile<MLBBaseballBatter>(path: _trainDataPath,
                hasHeader: true, separatorChar: ',', allowQuoting: false);
            var dataTest = _mlContext.Data.LoadFromTextFile<MLBBaseballBatter>(path: _testDataPath,
                hasHeader: true, separatorChar: ',', allowQuoting: false);
            var dataFull = _mlContext.Data.LoadFromTextFile<MLBBaseballBatter>(path: _fullDataPath,
                hasHeader: true, separatorChar: ',', allowQuoting: false);

            // TODO: REMOVE
            //dynamic myDynamic = new { PropertyOne = true, PropertyTwo = false };
            //var test = myDynamic.GetType();
            //var dynamicList = new List<dynamic>();
            //dynamicList.Add(myDynamic);
            //dynamicList.Add(myDynamic);
            //var test2 = _mlContext.Data.LoadFromEnumerable<dynamic>(dynamicList);
            //var pre = test2.Preview();
            //var testD = dataTrainBatters.FirstOrDefault();
            //Microsoft.ML.Data.SchemaDefinition sd;
            //var schemaDefinition = SchemaDefinition.Create(testD.GetType());
            //var test2 = _mlContext.Data.LoadFromEnumerable<dynamic>(dataTrainBatters, schemaDefinition );
            //var test2preview = test2.Preview();

            // Retrieve Data Schema
            var dataSchema = dataTrain.Schema;

            #if DEBUG
            // Debug Only: Preview the training/test data
            var dataTrainPreview = dataTrain.Preview();
            var dataTestPreview = dataTest.Preview();
            #endif

            // Cache the loaded data
            var cachedTrainData = _mlContext.Data.Cache(dataTrain);
            var cachedTestData = _mlContext.Data.Cache(dataTest);
            var cachedFullData = _mlContext.Data.Cache(dataFull);

            // Delete the Performance Metrics File(s)
            File.Delete(_performanceMetricsTrainTestModels);

            #endregion

            #region Step 2) Build Multiple Machine Learning Models

            // Notes:
            // Model training is for demo purposes and uses the default hyperparameters.
            // Default parameters were used in optimizing for large data sets.
            // It is best practice to always provide hyperparameters explicitly in order to have historical reproducability
            // as the ML.NET API evolves.

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("###############################");
            Console.WriteLine("Step 2: Train Models...");
            Console.WriteLine("###############################\n");
            Console.ResetColor();

            /* LIGHTGBM MODELS */
            Console.WriteLine("Training...LightGbm Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineLightGbmOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelLightGbmOnHallOfFameBallot = learningPipelineLightGbmOnHallOfFameBallot.Fit(cachedTrainData);
            var modelLightGbmOnHallOfFameBallotFull = learningPipelineLightGbmOnHallOfFameBallot.Fit(cachedFullData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LightGbm", _labelColunmn, modelLightGbmOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "LightGbm", _labelColunmn, modelLightGbmOnHallOfFameBallot, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "LightGbm", _labelColunmn, modelLightGbmOnHallOfFameBallotFull);
            Utilities.SaveOnnxModel(true, appFolder, "LightGbm", _labelColunmn, modelLightGbmOnHallOfFameBallotFull, _mlContext, cachedFullData);


            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineLightGbmInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: _labelColunmn)
                );

            // Fit (build a Machine Learning Model)
            var modelLightGbmInductedToHallOfFame = learningPipelineLightGbmInductedToHallOfFame.Fit(cachedTrainData);
            var modelLightGbmInductedToHallOfFameFull = learningPipelineLightGbmInductedToHallOfFame.Fit(cachedFullData);
            // Save the models to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LightGbm", _labelColunmn, modelLightGbmInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "LightGbm", _labelColunmn, modelLightGbmInductedToHallOfFame, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "LightGbm", _labelColunmn, modelLightGbmInductedToHallOfFameFull);
            Utilities.SaveOnnxModel(true, appFolder, "LightGbm", _labelColunmn, modelLightGbmInductedToHallOfFameFull, _mlContext, cachedFullData);

            /* LOGISTIC REGRESSION MODELS */
            Console.WriteLine("Training...Logistic Regression Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineLogisticRegressionOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelLogisticRegressionOnHallOfFameBallot = learningPipelineLogisticRegressionOnHallOfFameBallot.Fit(cachedTrainData);

            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LogisticRegression", _labelColunmn, modelLogisticRegressionOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "LogisticRegression", _labelColunmn, modelLogisticRegressionOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineLogisticRegressionInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelLogisticRegressionInductedToHallOfFame = learningPipelineLogisticRegressionInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LogisticRegression", _labelColunmn, modelLogisticRegressionInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "LogisticRegression", _labelColunmn, modelLogisticRegressionInductedToHallOfFame, _mlContext, cachedTrainData);


            /* AVERAGED PERCEPTRON MODELS */
            Console.WriteLine("Training...Averaged Perceptron Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineAveragedPerceptronOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.AveragedPerceptron(labelColumnName: _labelColunmn, numberOfIterations: 10)
                );
            // Fit (build a Machine Learning Model)
            var modelAveragedPerceptronOnHallOfFameBallot = learningPipelineAveragedPerceptronOnHallOfFameBallot.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "AveragedPerceptron", _labelColunmn, modelAveragedPerceptronOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "AveragedPerceptron", _labelColunmn, modelAveragedPerceptronOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineAveragedPerceptronInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.AveragedPerceptron(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelAveragedPerceptronInductedToHallOfFame = learningPipelineAveragedPerceptronInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "AveragedPerceptron", _labelColunmn, modelAveragedPerceptronInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "AveragedPerceptron", _labelColunmn, modelAveragedPerceptronInductedToHallOfFame, _mlContext, cachedTrainData);


            /* FAST FOREST MODELS */
            Console.WriteLine("Training...Fast Forest Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineFastForestOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FastForest(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelFastForestOnHallOfFameBallot = learningPipelineFastForestOnHallOfFameBallot.Fit(cachedTrainData);
            var modelFastForestOnHallOfFameBallotFull = learningPipelineFastForestOnHallOfFameBallot.Fit(cachedFullData);

            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FastForest", _labelColunmn, modelFastForestOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "FastForest", _labelColunmn, modelFastForestOnHallOfFameBallot, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "FastForest", _labelColunmn, modelFastForestOnHallOfFameBallotFull);
            Utilities.SaveOnnxModel(true, appFolder, "FastForest", _labelColunmn, modelFastForestOnHallOfFameBallotFull, _mlContext, cachedFullData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineFastForestInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FastForest(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelFastForestInductedToHallOfFame = learningPipelineFastForestInductedToHallOfFame.Fit(cachedTrainData);
            var modelFastForestInductedToHallOfFameFull = learningPipelineFastForestInductedToHallOfFame.Fit(cachedFullData);

            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FastForest", _labelColunmn, modelFastForestInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "FastForest", _labelColunmn, modelFastForestInductedToHallOfFame, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "FastForest", _labelColunmn, modelFastForestInductedToHallOfFameFull);
            Utilities.SaveOnnxModel(true, appFolder, "FastForest", _labelColunmn, modelFastForestInductedToHallOfFameFull, _mlContext, cachedTrainData);


            /* FAST TREE MODELS */
            Console.WriteLine("Training...Fast Tree Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineFastTreeOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FastTree(labelColumnName: _labelColunmn, learningRate: 0.01, numberOfTrees: 500)
                );
            // Fit (build a Machine Learning Model)
            var modelFastTreeOnHallOfFameBallot = learningPipelineFastTreeOnHallOfFameBallot.Fit(cachedTrainData);
            var modelFastTreeOnHallOfFameBallotFull = learningPipelineFastTreeOnHallOfFameBallot.Fit(cachedFullData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FastTree", _labelColunmn, modelFastTreeOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "FastTree", _labelColunmn, modelFastTreeOnHallOfFameBallot, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "FastTree", _labelColunmn, modelFastTreeOnHallOfFameBallotFull);
            Utilities.SaveOnnxModel(true, appFolder, "FastTree", _labelColunmn, modelFastTreeOnHallOfFameBallotFull, _mlContext, cachedFullData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineFastTreeInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FastTree(labelColumnName: _labelColunmn, learningRate: 0.01, numberOfTrees: 500)
                );
            // Fit (build a Machine Learning Model)
            var modelFastTreeInductedToHallOfFame = learningPipelineFastTreeInductedToHallOfFame.Fit(cachedTrainData);
            var modelFastTreeInductedToHallOfFameFull = learningPipelineFastTreeInductedToHallOfFame.Fit(cachedFullData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FastTree", _labelColunmn, modelFastTreeInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "FastTree", _labelColunmn, modelFastTreeInductedToHallOfFame, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "FastTree", _labelColunmn, modelFastTreeInductedToHallOfFameFull);
            Utilities.SaveOnnxModel(true, appFolder, "FastTree", _labelColunmn, modelFastTreeInductedToHallOfFameFull, _mlContext, cachedFullData);


            /* FIELD AWARE FACTORIZATION MODELS */
            Console.WriteLine("Training...Field Aware Factorization Models.");
            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineFieldAwareFactorizationOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FieldAwareFactorizationMachine(featureColumnNames: new[] { "Features" }, labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelFieldAwareFactorizationOnHallOfFameBallot = learningPipelineFieldAwareFactorizationOnHallOfFameBallot.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FieldAwareFactorization", _labelColunmn, modelFieldAwareFactorizationOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "FieldAwareFactorization", _labelColunmn, modelFieldAwareFactorizationOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineFieldAwareFactorizationInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.FieldAwareFactorizationMachine(featureColumnNames: new[] { "Features" }, labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelFieldAwareFactorizationInductedToHallOfFame = learningPipelineFieldAwareFactorizationInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "FieldAwareFactorization", _labelColunmn, modelFieldAwareFactorizationInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "FieldAwareFactorization", _labelColunmn, modelFieldAwareFactorizationInductedToHallOfFame, _mlContext, cachedTrainData);


            /* STOCHASTIC GRADIENT DESCENT - CALIBRATED MODELS */
            Console.WriteLine("Training...Stochastic Gradient Descent - Calibrated Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineStochasticGradientDescentCalibratedOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.SgdCalibrated(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelStochasticGradientDescentCalibratedOnHallOfFameBallot = learningPipelineStochasticGradientDescentCalibratedOnHallOfFameBallot.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "StochasticGradientDescentCalibrated", _labelColunmn, modelStochasticGradientDescentCalibratedOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "StochasticGradientDescentCalibrated", _labelColunmn, modelStochasticGradientDescentCalibratedOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineStochasticGradientDescentCalibratedInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.SgdCalibrated(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelStochasticGradientDescentCalibratedInductedToHallOfFame = learningPipelineStochasticGradientDescentCalibratedInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "StochasticGradientDescentCalibrated", _labelColunmn, modelStochasticGradientDescentCalibratedInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "StochasticGradientDescentCalibrated", _labelColunmn, modelStochasticGradientDescentCalibratedInductedToHallOfFame, _mlContext, cachedTrainData);

            /* STOCHASTIC GRADIENT DESCENT - NON CALIBRATED MODELS */
            Console.WriteLine("Training...Stochastic Gradient Descent - NonCalibrated Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineStochasticGradientDescentNonCalibratedOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.SgdNonCalibrated(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelStochasticGradientDescentNonCalibratedOnHallOfFameBallot = learningPipelineStochasticGradientDescentNonCalibratedOnHallOfFameBallot.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "StochasticGradientDescentNonCalibrated", _labelColunmn, modelStochasticGradientDescentNonCalibratedOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "StochasticGradientDescentNonCalibrated", _labelColunmn, modelStochasticGradientDescentNonCalibratedOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineStochasticGradientDescentNonCalibratedInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.SgdNonCalibrated(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelStochasticGradientDescentNonCalibratedInductedToHallOfFame = learningPipelineStochasticGradientDescentNonCalibratedInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "StochasticGradientDescentNonCalibrated", _labelColunmn, modelStochasticGradientDescentNonCalibratedInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "StochasticGradientDescentNonCalibrated", _labelColunmn, modelStochasticGradientDescentNonCalibratedInductedToHallOfFame, _mlContext, cachedTrainData);


            // TODO: Fix for PFI
            //var transformedData = modelStochasticGradientDescentInductedToHallOfFame.Transform(cachedTrainData);
            //var permutationFeatureImportance =
            //_mlContext.BinaryClassification.PermutationFeatureImportance(modelStochasticGradientDescentInductedToHallOfFame.LastTransformer, 
            //data: transformedData, labelColumnName: _labelColunmn);
            //Microsoft.ML.Data.TransformerChain<Microsoft.ML.Data.BinaryPredictionTransformer<Microsoft.ML.Calibrators.CalibratedModelParametersBase<Microsoft.ML.Trainers.LinearBinaryModelParameters, Microsoft.ML.Calibrators.PlattCalibrator>>> test;
            //test = modelStochasticGradientDescentInductedToHallOfFame;
            //test = null;
            //var loadedModelTest = Utilities.LoadModel(_mlContext,
            //    Utilities.GetModelPath(appFolder, algorithmName: algorithmsForModelExplainability[0], isOnnx: false, label: _labelColunmn));


            /* GENERALIZED ADDITIVE MODELS */
            Console.WriteLine("Training...Generalized Additive Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineGeneralizedAdditiveModelsOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.Gam(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelGeneralizedAdditiveModelsOnHallOfFameBallot = learningPipelineGeneralizedAdditiveModelsOnHallOfFameBallot.Fit(cachedTrainData);
            var modelGeneralizedAdditiveModelsOnHallOfFameBallotFull = learningPipelineGeneralizedAdditiveModelsOnHallOfFameBallot.Fit(cachedFullData);
            // Save the models to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsOnHallOfFameBallot, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsOnHallOfFameBallotFull);
            Utilities.SaveOnnxModel(true, appFolder, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsOnHallOfFameBallotFull, _mlContext, cachedFullData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineGeneralizedAdditiveModelsInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.Gam(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelGeneralizedAdditiveModelsInductedToHallOfFame = learningPipelineGeneralizedAdditiveModelsInductedToHallOfFame.Fit(cachedTrainData);
            var modelGeneralizedAdditiveModelsInductedToHallOfFameFull = learningPipelineGeneralizedAdditiveModelsInductedToHallOfFame.Fit(cachedFullData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsInductedToHallOfFame, _mlContext, cachedTrainData);
            Utilities.SaveModel(true, appFolder, _mlContext, dataSchema, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsInductedToHallOfFameFull);
            Utilities.SaveOnnxModel(true, appFolder, "GeneralizedAdditiveModels", _labelColunmn, modelGeneralizedAdditiveModelsInductedToHallOfFameFull, _mlContext, cachedFullData);


            /* LINEAR SUPPORT VECTOR MODELS */
            Console.WriteLine("Training...Linear Support Vector Models.");

            _labelColunmn = "OnHallOfFameBallot";
            // Build simple data pipeline
            var learningPipelineLinearSupportVectorMachinesOnHallOfFameBallot =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LinearSvm(labelColumnName: _labelColunmn, numberOfIterations: 10)
                );
            // Fit (build a Machine Learning Model)
            var modelLinearSupportVectorMachinesOnHallOfFameBallot = learningPipelineLinearSupportVectorMachinesOnHallOfFameBallot.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LinearSupportVectorMachines", _labelColunmn, modelLinearSupportVectorMachinesOnHallOfFameBallot);
            Utilities.SaveOnnxModel(false, appFolder, "LinearSupportVectorMachines", _labelColunmn, modelLinearSupportVectorMachinesOnHallOfFameBallot, _mlContext, cachedTrainData);

            _labelColunmn = "InductedToHallOfFame";
            // Build simple data pipeline
            var learningPipelineLinearSupportVectorMachinesInductedToHallOfFame =
                Utilities.GetBaseLinePipeline(_mlContext, featureColumns).Append(
                _mlContext.BinaryClassification.Trainers.LinearSvm(labelColumnName: _labelColunmn)
                );
            // Fit (build a Machine Learning Model)
            var modelLinearSupportVectorMachinesInductedToHallOfFame = learningPipelineLinearSupportVectorMachinesInductedToHallOfFame.Fit(cachedTrainData);
            // Save the model to storage
            Utilities.SaveModel(false, appFolder, _mlContext, dataSchema, "LinearSupportVectorMachines", _labelColunmn, modelLinearSupportVectorMachinesInductedToHallOfFame);
            Utilities.SaveOnnxModel(false, appFolder, "LinearSupportVectorMachines", _labelColunmn, modelLinearSupportVectorMachinesInductedToHallOfFame, _mlContext, cachedTrainData);


            //var test = _mlContext.BinaryClassification.CrossValidate(cachedTrainData, learningPipelineLightGbmInductedToHallOfFame, 100,
            //    labelColumn: _labelColunmn, stratificationColumn: _labelColunmn);

            Console.WriteLine(string.Empty);

            #endregion

            #region Step 3) Report Performance Metrics

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("###############################");
            Console.WriteLine("Step 3: Report Metrics...");
            Console.WriteLine("###############################\n");
            Console.ResetColor();

            // Write the performance metrics HEADER
            var performanceMetricsTrainTestHeaderRow = $@"{"AlgorithmName"},{"LabelColumn"},{"Seed"},{"F1Score"},{"AreaUnderPrecisionRecallCurve"},{"AreaUnderRocCurve"},{"PositivePrecision"},{"PositiveRecall"},{"Accuracy"},{"LogLoss"}";
            using (System.IO.StreamWriter file = File.AppendText(_performanceMetricsTrainTestModels))
            {
                file.WriteLine(performanceMetricsTrainTestHeaderRow);
            }

            for (int i = 1; i < algorithmsForModelExplainability.Length; i++)
            {
                for (int j = 0; j < labelColumns.Length; j++)
                {
                    // TRAIN/TEST MODEL PERFORMANCE METRICS
                    var isFinalModel = false;
                    var binaryClassificationMetrics = Utilities.GetBinaryClassificationModelMetrics(isFinalModel, appFolder, _mlContext, labelColumns[j], algorithmsForModelExplainability[i], cachedTestData);

                    var metricF1Score = Math.Round(binaryClassificationMetrics.F1Score, 4);
                    var metricAreaUnderPrecisionRecallCurve = Math.Round(binaryClassificationMetrics.AreaUnderPrecisionRecallCurve, 4);
                    var metricAreaUnderRocCurve = Math.Round(binaryClassificationMetrics.AreaUnderRocCurve, 4);
                    var metricPositivePrecision = Math.Round(binaryClassificationMetrics.PositivePrecision, 4);
                    var metricPositiveRecall = Math.Round(binaryClassificationMetrics.PositiveRecall, 4);
                    var metricAccuracy = Math.Round(binaryClassificationMetrics.Accuracy, 4);
                    var metricLogLoss = Math.Round(binaryClassificationMetrics.LogLoss, 4);

                    Console.WriteLine("TRAIN/TEST Performance Metrics for " + algorithmsForModelExplainability[i] + " | " + labelColumns[j]);
                    Console.WriteLine("**************************");
                    Console.WriteLine("F1 Score:                 " + metricF1Score);
                    Console.WriteLine("AUC - Prec/Recall Score:  " + metricAreaUnderPrecisionRecallCurve);
                    Console.WriteLine("AUC - ROC Score:          " + metricAreaUnderRocCurve);
                    Console.WriteLine("Precision:                " + metricPositivePrecision);
                    Console.WriteLine("Recall:                   " + metricPositiveRecall);
                    Console.WriteLine("Accuracy:                 " + metricAccuracy);
                    Console.WriteLine("LogLoss:                  " + metricLogLoss);
                    Console.WriteLine("**************************");

                    // Write the performance metrics to file
                    var performanceMetricsTrainTestRow = $@"{algorithmsForModelExplainability[i]},{labelColumns[j]},{seed},{metricF1Score},{metricAreaUnderPrecisionRecallCurve},{metricAreaUnderRocCurve},{metricPositivePrecision},{metricPositiveRecall},{metricAccuracy},{metricLogLoss}";
                    using (System.IO.StreamWriter file = File.AppendText(_performanceMetricsTrainTestModels))
                    {
                        file.WriteLine(performanceMetricsTrainTestRow);
                    }

                    // CROSSVALIDATED PERFORMANCE METRICS
                    // TODO


                    var loadedModel = Utilities.LoadModel(_mlContext, Utilities.GetModelPath(appFolder, algorithmName: algorithmsForModelExplainability[i], isOnnx: false, label: labelColumns[j], isFinalModel: false));
                    ITransformer transfomer = (ITransformer) loadedModel;

                    ITransformer lModel = loadedModel;
                    //_mlContext.BinaryClassification.PermutationFeatureImportance(lModel, transformedModelData);

                    var lastTran = loadedModel.LastTransformer;
                    //var enumerator = lastTran.GetEnumerator();

                    // TODO: Check for PFI support
                    ISingleFeaturePredictionTransformer<ModelParametersBase<float>> transfomerForPfi = null; // lastTran;
                    //   (ISingleFeaturePredictionTransformer<ModelParametersBase<float>>) lastTran;


                    //_mlContext.BinaryClassification.PermutationFeatureImportance(modelStochasticGradientDescentInductedToHallOfFame.LastTransformer, data: transformedData, labelColumnName: _labelColunmn);
                    //_mlContext.BinaryClassification.PermutationFeatureImportance(lastTran, data: cachedTrainData, labelColumnName: labelColumns[j]);

                    //if (transfomerForPfi != null)
                    //{
                    //    _mlContext.BinaryClassification.PermutationFeatureImportance(transfomerForPfi, transformedModelData);
                    //}


                    //ISingleFeaturePredictionTransformer<IPredictorProducing<float>> transfomerForPfi = null;
                    //while (enumerator.MoveNext())
                    //{
                    //    if (enumerator.Current is IPredictionTransformer<ModelParametersBase<float>>)
                    //    {
                    //        transfomerForPfi = enumerator.Current as ISingleFeaturePredictionTransformer<ModelParametersBase<float>>;
                    //    }
                    //}

                    if (transfomerForPfi != null)
                    {
                        // Console.WriteLine("!!!!!!!!HEELLO");
                        //_mlContext.BinaryClassification.PermutationFeatureImportance(loadedModel.LastTransformer, null);
                        //// TODO: FIX
                        //// Retrieve Top Features based on Permutation Feature Importance
                        //var permutationMetrics = _mlContext.BinaryClassification.PermutationFeatureImportance(model: loadedModel.LastTransformer, data: transformedModelData,
                        // label: labelColumns[j], features: "Features", useFeatureWeightFilter: false, permutationCount: 10);

                        //// Build a list of feature importance metrics
                        //List<FeatureImportanceValue> featureImportanceValues = new List<FeatureImportanceValue>();
                        //for (int k = 0; k < permutationMetrics.Length; k++)
                        //{
                        //    featureImportanceValues.Add(
                        //            new FeatureImportanceValue
                        //            {
                        //                FeatureName = featureColumns[k],
                        //                PerformanceMetricName = "F1Score.Mean",
                        //                PerformanceMetricValue = permutationMetrics[k].F1Score.Mean
                        //            }
                        //        );
                        //}

                        //// Filter out NaN values and order by lowest values
                        //// Note: Should be done with absolute and check for positive values for features
                        //var orderedFeatures = featureImportanceValues.Where(a => !Double.IsNaN(a.PerformanceMetricValue)).OrderBy(a => a.PerformanceMetricValue).ToList();
                        //var numberOfFeaturesToReport = 4;

                        //Console.WriteLine("Most important features (" + numberOfFeaturesToReport + ")");
                        //Console.WriteLine("******************");

                        //for (int l = 0; l < numberOfFeaturesToReport; l++)
                        //{
                        //    if (l + 1 <= featureImportanceValues.Count && l < orderedFeatures.Count)
                        //    {
                        //        Console.WriteLine(orderedFeatures[l].FeatureName + ": " + Math.Round(orderedFeatures[l].PerformanceMetricValue, 4).ToString());
                        //    }
                        //}
                    }
                    else
                    {
                        // TODO: FIX in post v1.0+

                        //Console.WriteLine("Most important features ()");
                        //Console.WriteLine("******************");
                        //Console.WriteLine("Model's algorithm does not support explainability.");
                    }

                    Console.WriteLine("**************************");
                    Console.WriteLine();
                }
            }



            #endregion

            #region Step 4) New Predictions - Using Ficticious Player Data

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("###############################");
            Console.WriteLine("Step 4: New Predictions...");
            Console.WriteLine("###############################\n");
            Console.ResetColor();

            // Set algorithm type to use for predictions
            // Retrieve model path
            // TODO: Hardcoded add perscriptive rules engine
            var algorithmTypeName = "FastTree";
            var loadedModelOnHallOfFameBallot = Utilities.LoadModel(_mlContext, (Utilities.GetModelPath(appFolder, algorithmTypeName, false, "OnHallOfFameBallot", true)));
            var loadedModelInductedToHallOfFame = Utilities.LoadModel(_mlContext, (Utilities.GetModelPath(appFolder, algorithmTypeName, false, "InductedToHallOfFame", true)));

            // Create prediction engine
            var predEngineOnHallOfFameBallot = _mlContext.Model.CreatePredictionEngine<MLBBaseballBatter, MLBHOFPrediction>(loadedModelOnHallOfFameBallot);
            var predEngineInductedToHallOfFame = _mlContext.Model.CreatePredictionEngine<MLBBaseballBatter, MLBHOFPrediction>(loadedModelInductedToHallOfFame);

            // Create statistics for bad, average & great player
            var badMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Bad Player",
                ID = 100f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 2f,
                AB = 100f,
                R = 10f,
                H = 30f,
                Doubles = 1f,
                Triples = 1f,
                HR = 1f,
                RBI = 10f,
                SB = 10f,
                BattingAverage = 0.3f,
                SluggingPct = 0.15f,
                AllStarAppearances = 1f,
                MVPs = 0f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 200f
            };
            var averageMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Average Player",
                ID = 100f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 2f,
                AB = 8393f,
                R = 1162f,
                H = 2340f,
                Doubles = 410f,
                Triples = 8f,
                HR = 439f,
                RBI = 1412f,
                SB = 9f,
                BattingAverage = 0.279f,
                SluggingPct = 0.486f,
                AllStarAppearances = 6f,
                MVPs = 0f,
                TripleCrowns = 0f,
                GoldGloves = 0f,
                MajorLeaguePlayerOfTheYearAwards = 0f,
                TB = 4083f
            };
            var greatMLBBatter = new MLBBaseballBatter
            {
                FullPlayerName = "Great Player",
                ID = 100f,
                InductedToHallOfFame = false,
                LastYearPlayed = 0f,
                OnHallOfFameBallot = false,
                YearsPlayed = 20f,
                AB = 10000f,
                R = 1900f,
                H = 3500f,
                Doubles = 500f,
                Triples = 150f,
                HR = 600f,
                RBI = 1800f,
                SB = 400f,
                BattingAverage = 0.350f,
                SluggingPct = 0.65f,
                AllStarAppearances = 14f,
                MVPs = 2f,
                TripleCrowns = 1f,
                GoldGloves = 4f,
                MajorLeaguePlayerOfTheYearAwards = 2f,
                TB = 7000f
            };

            var batters = new List<MLBBaseballBatter> { badMLBBatter, averageMLBBatter, greatMLBBatter };
            // Convert the list to an IDataView
            var newPredictionsData = _mlContext.Data.LoadFromEnumerable(batters);

            // Make the predictions for both OnHallOfFameBallot & InductedToHallOfFame
            var predBadOnHallOfFameBallot = predEngineOnHallOfFameBallot.Predict(badMLBBatter);
            var predBadInductedToHallOfFame = predEngineInductedToHallOfFame.Predict(badMLBBatter);
            var predAverageOnHallOfFameBallot = predEngineOnHallOfFameBallot.Predict(averageMLBBatter);
            var predAverageInductedToHallOfFame = predEngineInductedToHallOfFame.Predict(averageMLBBatter);
            var predGreatOnHallOfFameBallot = predEngineOnHallOfFameBallot.Predict(greatMLBBatter);
            var predGreatInductedToHallOfFame = predEngineInductedToHallOfFame.Predict(greatMLBBatter);

            // Report the results
            Console.WriteLine("Algorithm Used for sample Model Prediction: " + algorithmTypeName);
            Console.WriteLine("\n");
            Console.WriteLine("Bad Baseball Player Prediction");
            Console.WriteLine("------------------------------");
            Console.WriteLine("On HOF Ballot Prediction: " + predBadOnHallOfFameBallot.Prediction.ToString() + " | " + "Probability: " + predBadOnHallOfFameBallot.Probability);
            Console.WriteLine("HOF Inducted Prediction:  " + predBadInductedToHallOfFame.Prediction.ToString() + " | " + "Probability: " + predBadInductedToHallOfFame.Probability);
            Console.WriteLine();
            Console.WriteLine("Average Baseball Player Prediction");
            Console.WriteLine("------------------------------");
            Console.WriteLine("On HOF Ballot Prediction: " + predAverageOnHallOfFameBallot.Prediction.ToString() + " | " + "Probability: " + predAverageOnHallOfFameBallot.Probability);
            Console.WriteLine("HOF Inducted Prediction:  " + predAverageInductedToHallOfFame.Prediction.ToString() + " | " + "Probability: " + predAverageInductedToHallOfFame.Probability);
            Console.WriteLine();
            Console.WriteLine("Great Baseball Player Prediction");
            Console.WriteLine("------------------------------");
            Console.WriteLine("On HOF Ballot Prediction: " + predGreatOnHallOfFameBallot.Prediction.ToString() + " | " + "Probability: " + predGreatOnHallOfFameBallot.Probability);
            Console.WriteLine("HOF Inducted Prediction:  " + predGreatInductedToHallOfFame.Prediction.ToString() + " | " + "Probability: " + predGreatInductedToHallOfFame.Probability);

            #endregion

            // End of job, report time
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format("Finished Baseball Predictions - Training Model Job in: {0} seconds", Math.Round(sw.Elapsed.TotalSeconds, 2)));
            Console.ReadLine();
        }
    }
}
