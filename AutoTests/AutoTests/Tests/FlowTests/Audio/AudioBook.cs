namespace FileFlowsTests.Tests.FlowTests.Audio;

public class AudioBook : AudioTest
{
    /// <summary>
    /// Manually creates an audiobook flow and tests it
    /// </summary>
    [Test, Order(1)]
    public async Task CreateBookManually()
    {
        // create flow
        var flowName = RandomName("Create Audio Book");
        const string BOOK_NAME = "How To Use FileFlows";
        string OUTPUT_PATH = Path.Combine(TempPath, flowName, BOOK_NAME + ".m4b");
            
        string libName = "Library For " + flowName;
        await CreateFlow(flowName, "Folder", []);

        await FileFlows.Flow.AddFlowElement("FileFlows.AudioNodes.CreateAudioBook", 250, 220);

        await SetText("DestinationPath", Path.Combine(TempPath, flowName, "{folder.Name}.m4b"));
        await ButtonClick("Save");

        await FileFlows.Flow.Connect("Input Folder", "Create Audio Book");

        await FileFlows.Flow.Save();

        // Create library
        string libPath = await CreateAudioBookLibrary(BOOK_NAME);
        await CreateFolderLibrary(libName, flowName, libPath: libPath, scan: true);
        
        // Test processing
        await TestBookExists(BOOK_NAME, OUTPUT_PATH);
    }

    /// <summary>
    /// Creates a book replacing the original
    /// </summary>
    [Test, Order(1)]
    public async Task CreateBook_Template_ReplaceOriginal()
    {
        // create flow
        var flowName = RandomName("Audio Book Replace Original");
        const string BOOK_NAME = "The Book Replacer";
            
        string libName = "Library For " + flowName;
        await CreateFlow(flowName, "Create Audio Book", [
            new("Output Path", "Replace Original", InputType.Select),
            new("DeleteSourceFiles1", true, InputType.Toggle)
        ]);

        // Create library
        string libPath = await CreateAudioBookLibrary(BOOK_NAME);
        await CreateFolderLibrary(libName, flowName, libPath: libPath, scan: true);
        string outputPath = $"{libPath}/{BOOK_NAME}.m4b";
        
        // Test processing
        await TestBookExists(BOOK_NAME, outputPath);
        Assert.IsFalse(Directory.Exists(libPath), "Failed to delete original files");
    }
    

    /// <summary>
    /// Creates a book replacing the original
    /// </summary>
    [Test, Order(1)]
    public async Task CreateBook_Template_SaveToFolder()
    {
        // create flow
        var flowName = RandomName("Audio Book Save To Folder");
        const string BOOK_NAME = "The Book in the new Folder";

        string destFolder = Path.Combine(TempPath, Guid.NewGuid().ToString());
        
        string libName = "Library For " + flowName;
        await CreateFlow(flowName, "Create Audio Book", [
            new("Output Path", "Save To Folder", InputType.Select),
            new("Destination Path", destFolder, InputType.Text),
            new("DeleteSourceFiles2", false, InputType.Toggle)
        ]);

        // Create library
        string libPath = await CreateAudioBookLibrary(BOOK_NAME);
        await CreateFolderLibrary(libName, flowName, libPath: libPath, scan: true);
        string outputPath = $"{destFolder}/{BOOK_NAME}.m4b";
        
        // Test processing
        await TestBookExists(BOOK_NAME, outputPath);
        Assert.IsTrue(Directory.Exists(libPath), "Original files no longer exist when they should.");
    }
    
    private async Task TestBookExists(string bookName, string outputPath)
    {
        await GotoPage("Files");
        await SkyBox("Processed");
        await Task.Delay(5_000);
        DateTime end = DateTime.Now.AddMinutes(2);
        while (end > DateTime.Now)
        {
            await SkyBox("Processed");
            if (await ItemExists(bookName))
                break;
        
            await Task.Delay(1000);
        }
        
        await DoubleClickItem(bookName);

        string log = await DownloadLog();
        Logger.ILog(new string('-', 100) + Environment.NewLine + log);
        Logger.ILog(new string('-', 100));

        Assert.IsTrue(File.Exists(outputPath), "Book failed to be created: " + outputPath);
    }

    private async Task<string> CreateAudioBookLibrary(string bookName)
    {
        var libPath =Path.Combine(TempPath, "lib-audio-book-" + Guid.NewGuid());
        var bookFolder = Path.Combine(libPath, bookName);
        Directory.CreateDirectory(bookFolder);
        await GenerateRandomAudio(Path.Combine(bookFolder, "01. Introduction.mp3"));
        await GenerateRandomAudio(Path.Combine(bookFolder, "02. Flows.mp3"));
        await GenerateRandomAudio(Path.Combine(bookFolder, "03. Libraries.mp3"));
        await GenerateRandomAudio(Path.Combine(bookFolder, "04. Variables.mp3"));
        await GenerateRandomAudio(Path.Combine(bookFolder, "05. Conclusion.mp3"));
        return libPath;
    }
}