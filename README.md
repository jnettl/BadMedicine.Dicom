# BadMedicine.Dicom

[![NuGet Badge](https://buildstats.info/nuget/HIC.BadMedicine.Dicom)](https://www.nuget.org/packages/HIC.BadMedicine.Dicom/) [![Build, test and package](https://github.com/SMI/BadMedicine.Dicom/actions/workflows/testpack.yml/badge.svg)](https://github.com/SMI/BadMedicine.Dicom/actions/workflows/testpack.yml) [![CodeQL](https://github.com/SMI/BadMedicine.Dicom/actions/workflows/codeql.yml/badge.svg)](https://github.com/SMI/BadMedicine.Dicom/actions/workflows/codeql.yml)

The purpose of BadMedicine.Dicom is to generate large volumes of complex (in terms of tags) dicom images for integration/stress testing ETL and image management tools.

There are a number of public sources of Dicom clinical images e.g. [TCIA ](https://www.cancerimagingarchive.net/).  The difficulty with using these for integration/stress testing is that they often:

- Are anonymised (majority of tags have been removed)
- Do not represent the breadth of Modalities/Tags found in a live clinical [PACS](https://en.wikipedia.org/wiki/Picture_archiving_and_communication_system).
- Take up a lot of space

BadMedicine.Dicom generates dicom images on demand based on an anonymous aggregate model of tag data found in scottish medical imaging.  It is an extension of [SynthEHR](https://github.com/SMI/SynthEHR) which generates traditional EHR records.

## Usage

BadDicom is available as a [nuget package](https://www.nuget.org/packages/HIC.BadMedicine.Dicom/) for linking as a library

The standalone CLI (BadDicom.exe) is available in the [releases section of Github](https://github.com/SMI/BadMedicine.Dicom/releases)

Usage is as follows:

```
BadDicom.exe c:\temp\testdicoms
```
_Generates 10 dicom studies (~700MB)_

```
BadDicom.exe c:\temp\testdicoms 5 10 --NoPixels
```
_Generates 10 dicom studies from a pool of 5 patients without pixel data (~3MB)_

You can pass `-s` to seed the random number generators.  Seeding will ensure the same StudyDate, PatientId etc get generated but will __not__ affect UIDs generated (UIDs are always unique)

```
BadDicom.exe c:\temp\testdicoms 5 10 --NoPixels -s 100
```

## Direct to Database

You can generate DICOM metadata directly into a relational database (instead of onto disk).  This can be done by downloading an [image template](https://github.com/SMI/DicomTypeTranslation/tree/master/Templates) or by [creating one yourself](https://github.com/SMI/DicomTemplateBuilder).

To turn this mode on rename the file `BadDicom.template.yaml` to `BadDicom.yaml` and provide the connection strings to your database e.g.:

```yaml
Database:
  # The connection string to your database
  ConnectionString: server=127.0.0.1;Uid=root;Pwd=;Ssl-Mode=None
  # Your DBMS provider ('MySql', 'PostgreSql','Oracle' or 'MicrosoftSQL')
  DatabaseType: MySql
  # Contains the table schema (which dicom tags to use for which tables)
  Template: CT.it
  # Database to create/use on the server
  DatabaseName: SynthEHRTestData
  # Setting this deduplicates study/series level schemas (works only if tables do not already exist on server)
  MakeDistinct: true
```

## EHR Datasets

If you want to generate EHR datasets with a shared patient pool with the dicom data (e.g. for doing linkage) you can provided the -s (seed) and use the main [SynthEHR](https://github.com/SMI/SynthEHR) application.

```
BadDicom.exe c:\temp\testdicoms 12 10 -s 100
SynthEHR.exe c:\temp\testdicoms 12 100 -s 100
```
_Generates a pool of 12 patients and 10 Studies (for random patients) then 100 rows of data for each EHR dataset_

# Library Usage
You can generate test data for your program yourself by referencing the [nuget package](https://www.nuget.org/packages/HIC.BadMedicine.Dicom/):

```csharp
//create a test person
var r = new Random(23);
var person = new Person(r);

//create a generator 
using (var generator = new DicomDataGenerator(r, null, "CT"))
{
    //create a dataset in memory
    DicomDataset dataset = generator.GenerateTestDataset(person, r);

    //values should match the patient details
    Assert.AreEqual(person.CHI,dataset.GetValue<string>(DicomTag.PatientID,0));
    Assert.GreaterOrEqual(dataset.GetValue<DateTime>(DicomTag.StudyDate,0),person.DateOfBirth);

    //should have a study description
    Assert.IsNotNull(dataset.GetValue<string>(DicomTag.StudyDescription,0));   
}
```

## Building

Building requires MSBuild 15 or later (or Visual Studio 2017 or later).  You will also need to install the DotNetCore 2.2 SDK.

Csproj files are in the 2017 format and require Visual Studio 2017 or later to run.  The following projects are part of the solution:

|Project | Runtime | Purpose | Build Output |
|-----|-----|-----|-----|
|BadDicom.csproj | Dot Net Core 2.2| Command Line Tool| BadDicom.exe generated by dotnet publish*|
|BadMedicine.Dicom.csproj | Dot Net Standard 2.0| Library | BadMedicine.Dicom.dll.  Use BadMedicine.Dicom.nuspec to upload to NuGet|
|BadMedicine.Dicom.Tests | Dot Net Core 2.2 | Tests for library | None |


_*Publish an OS specific binary by building BadDicom.csproj then running:_
```
dotnet publish BadDicom.csproj -r win-x64 --self-contained
cd .\bin\Debug\netcoreapp2.2\win-x64\
```

For Linux, a few extra requirements are needed:

```bash
# For Ubuntu
$ sudo apt install libc6-dev libgdiplus

# For CentOS
$ sudo yum install libc6-devel libgdiplus
```

# Tag Data

Basic random patient information (age, CHI etc) are generated by [SynthEHR](https://github.com/SMI/SynthEHR)

The following tags are populated in dicom files generated:

|Tag | Model |
|-----|-----|
| PatientID | The CHI number of the (random) patient|
| StudyDate | A random date during the patients lifetime |
| StudyTime | Random time of day with hours favoured during the middle of the working day.|
| SeriesDate | Same as StudyDate* |
| PatientAge | Age of patient at SeriesDate e.g. "032Y"|
| Modality | Random modality for which we have at least 1 image locally. (proportionate to modality popularity)|
| StudyDescription | Random description that exists in the modality (proportionate to frequency seen) |

*SeriesDate is always the same as Study Date (see `Seres` constructor), for secondary capture this should/could not be the case (we should look at how this corresponds in the PACS data we have)

# Pixel Data
Currently pixel data is written as a black square with the SOP Instance UID written in white.

