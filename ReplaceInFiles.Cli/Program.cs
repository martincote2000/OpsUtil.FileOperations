﻿// See https://aka.ms/new-console-template for more information
using FluentArgs;
using ReplaceInFiles;
using Serilog;
using Spinnerino;
using System;
using System.Diagnostics;
using System.IO.Abstractions;

Stopwatch stopwatch = Stopwatch.StartNew();

Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();

//Global catch all error
AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;



Log.Logger.Information("Replace in files starting .....");

FluentArgsBuilder.New()
    .DefaultConfigsWithAppDescription("Replace values or parameter ${} in files.")
    .Parameter<bool>("--verbose")
        .WithDescription("Verbose mode")
        .IsOptional()
    .Parameter<bool>("--ignorecase")
        .WithDescription("Indicate to find parameters using case-insensitive")
        .IsOptionalWithDefault(false)
    .Parameter<bool>("--nopattern")
        .WithDescription("Specify to search the exact string of the parameter name specified.")
        .WithExamples("http://localhost/api/myspecialApi=MyValue1    ")
        .IsOptional()
    .Parameter<bool>("--includesubfolder")
        .WithDescription("Include sub folder in the search")
        .IsOptionalWithDefault(true)
    .Parameter<int>("--parallelexecution")
        .WithDescription("Parallels replacement")
        .WithValidation(n => n >= 1 && n <= 10, "Should be between 1 & 10")
        .IsOptionalWithDefault(5)
    .ListParameter("--ignorefoldernames")
        .WithDescription("A list of folder names to ignore (ex: bin, obj, .git).")
        .WithValidation(n => !string.IsNullOrWhiteSpace(n), "A name must not only contain whitespace.")
        .IsRequired()
    .ListParameter("-p", "--parameters")
        .WithDescription("List of parameters to find and replace in files. Parameter in the file is ${...variable name...}.")
        .WithExamples("--parameters \"ParameterName1=MyValue1;ParameterName2=MyValue2;\"")
        .WithValidation(n => !string.IsNullOrWhiteSpace(n), "Parameter should not be empty")
        .IsRequired()
    .ListParameter("-e", "--extensions")
        .WithDescription("A list of file extensions.")
        .WithValidation(n => !string.IsNullOrWhiteSpace(n), "Name should not be empty")
        .IsRequired()
    .Parameter("-f", "--folder")
        .WithDescription("Folder to search files")
        .WithExamples("C:\\MyFolder")
        .IsRequired()
    .Call(folder => extensions => replaceparameters => ignorefolderNames => parallelexecution => searchInsubfolder => nopattern => ignorecase => verbose =>
    {
        IFileSystem fileSystem = new FileSystem();

        Log.Logger.Information("Searching files ...");
        var filesFound = new FileSearcher(fileSystem)
            .InDirectory(folder)
            .WithExtensions(extensions?.ToArray())
            .ParallelsExecution(parallelexecution)
            .IncludeSubfolders(searchInsubfolder)
            .IgnoreFolderNames(ignorefolderNames?.ToArray())
            .Search();

        Log.Logger.Information("Number of file founds: {0}", filesFound.Count);

        if (filesFound.Any())
        {
            Log.Logger.Information("Replacement starting ... ");

            using (var bar = new InlineProgressBar())
            {
                var replacer = new FileReplacer(Log.Logger, fileSystem)
                    .ForFiles(filesFound)
                    .ParallelsExecution(parallelexecution)
                    .VerboseMode(verbose)
                    .IgnoreCase(ignorecase)
                    .ReportProgress(200, (fileCount, fileProcessed) =>
                    {
                        var progressPercentage = Math.Round((fileProcessed / fileCount) * 100);                      
                        bar.SetProgress(progressPercentage);
                    })
                    .ReplaceVariable(replaceparameters?.ToArray());

                if (nopattern == true)
                {
                    replacer.MatchPattern(string.Empty, string.Empty);
                }

                replacer.Replace();
            }
        }

    })
    .Parse(args);

Log.Logger.Information("Execution time {0}s", stopwatch.Elapsed.TotalSeconds);


void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
{
    var exception = e.ExceptionObject as Exception;
    Log.Logger.Error(exception, exception.Message);
}