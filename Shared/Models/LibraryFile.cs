using System.Xml.XPath;
using NPoco;

namespace FileFlows.Shared.Models;

using FileFlows.Plugin;
using System;
using System.Collections.Generic;

/// <summary>
/// A library file is a file that FileFlows will process
/// </summary>
[TableName(nameof(LibraryFile))]
public class LibraryFile : FileFlowObject
{
    /// <summary>
    /// Gets or sets the relative path of the library file.
    /// This is the path relative to the library
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// Gets or sets the path of the final output file
    /// </summary>
    public string OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the flow that executed this file
    /// </summary>
    [Ignore]
    public ObjectReference Flow 
    {
        get
        {
            if (FlowUid == null)
                return new ObjectReference() { };
            return new ObjectReference()
            {
                Uid = FlowUid.Value, Name = FlowName, Type = typeof(Flow).FullName
            };
        }
        set
        {
            FlowUid = value?.Uid;
            FlowName = value?.Name;
        }
    }

    public Guid? FlowUid { get; set; }

    public string FlowName { get; set; }

    /// <summary>
    /// Gets or sets the library this library files belongs to
    /// </summary>
    [Ignore]
    public ObjectReference Library 
    {
        get
        {
            if (LibraryUid == null)
                return new ObjectReference() { };
            return new ObjectReference()
            {
                Uid = LibraryUid.Value, Name = LibraryName, Type = typeof(Library).FullName
            };
        }
        set
        {
            LibraryUid = value?.Uid;
            LibraryName = value?.Name;
        }
    }

    public Guid? LibraryUid { get; set; }

    public string LibraryName { get; set; }
    
    /// <summary>
    /// Gets or sets an object reference to an existing
    /// library file that this file is a duplicate of
    /// </summary>
    [Ignore]
    public ObjectReference Duplicate
    {
        get
        {
            if (DuplicateUid == null)
                return new ObjectReference() { };
            return new ObjectReference()
            {
                Uid = DuplicateUid.Value, Name = DuplicateName, Type = typeof(LibraryFile).FullName
            };
        }
        set
        {
            DuplicateUid = value?.Uid;
            DuplicateName = value?.Name;
        }
    }

    public Guid? DuplicateUid { get; set; }

    public string DuplicateName { get; set; }

    /// <summary>
    /// Gets or sets the size of the original library file
    /// </summary>
    public long OriginalSize { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the final file after processing
    /// </summary>
    public long FinalSize { get; set; }
    
    /// <summary>
    /// Gets or sets the fingerprint of the file
    /// </summary>
    public string Fingerprint { get; set; }

    /// <summary>
    /// Gets or sets the node tha this processing/has processed the file
    /// </summary>
    [Ignore]
    public ObjectReference Node
    {
        get
        {
            if (NodeUid == null)
                return new ObjectReference() { };
            return new ObjectReference()
            {
                Uid = NodeUid.Value, Name = NodeName, Type = typeof(ProcessingNode).FullName
            };
        }
        set
        {
            NodeUid = value?.Uid;
            NodeName = value?.Name;
        }
    }

    public Guid? NodeUid { get; set; }

    public string NodeName { get; set; }

    /// <summary>
    /// Gets or sets the UID of the worker that is executing this library file
    /// </summary>
    public Guid? WorkerUid { get; set; }
    
    /// <summary>
    /// Gets or sets when the file began processing
    /// </summary>
    public DateTime ProcessingStarted { get; set; }
    
    /// <summary>
    /// Gets or sets when the file finished processing
    /// </summary>
    public DateTime ProcessingEnded { get; set; }

    /// <summary>
    /// Gets or sets the processing status of the file
    /// </summary>
    public FileStatus Status { get; set; }
    
    /// <summary>
    /// Gets or sets if the file no longer exists after it was processed
    /// </summary>
    public bool NoLongerExistsAfterProcessing { get; set; }
    
    /// <summary>
    /// Gets or sets the order of the file when the file should be processed
    /// </summary>
    [Column("ProcessingOrder")]
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets if this library file is a directory
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// Gets the total processing time of the library file
    /// </summary>
    [Ignore]
    public TimeSpan ProcessingTime
    {
        get
        {
            if (Status == FileStatus.Unprocessed)
                return new TimeSpan();
            if (Status == FileStatus.Processing)
                return DateTime.Now.Subtract(ProcessingStarted);
            if (ProcessingEnded < new DateTime(2000, 1, 1))
                return new TimeSpan();
            return ProcessingEnded.Subtract(ProcessingStarted);
        }
    }
    
    /// <summary>
    /// Gets or sets the data to hold this file for
    /// </summary>
    public DateTime HoldUntil { get; set; }
    
    /// <summary>
    /// Gets or sets when the file was created
    /// </summary>
    public DateTime CreationTime { get; set; }
    
    /// <summary>
    /// Gets or sets when the file was written to
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    /// Gets or sets a list of nodes that were executed against this library file
    /// </summary>
    [SerializedColumn]
    public List<ExecutedNode> ExecutedNodes { get; set; }
    
    /// <summary>
    /// Gets or sets the original metadata for a file
    /// </summary>
    [Column]
    public Dictionary<string, object> OriginalMetadata { get; set; }
    
    /// <summary>
    /// Gets or sets the final metadata for the file
    /// </summary>
    [Column]
    public Dictionary<string, object> FinalMetadata { get; set; }

}

/// <summary>
/// Possible status of library files
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// The file is on hold by its libraries hold interval
    /// </summary>
    OnHold = -3,
    /// <summary>
    /// The library is disabled and the file will not be processed
    /// </summary>
    Disabled = -2,
    /// <summary>
    /// The library is out of schedule and will not process until it is in the processing schedule
    /// </summary>
    OutOfSchedule = -1,
    /// <summary>
    /// The file has not been processed
    /// </summary>
    Unprocessed = 0,
    /// <summary>
    /// The file has been successfully processed
    /// </summary>
    Processed = 1,
    /// <summary>
    /// The file is currently processing
    /// </summary>
    Processing = 2,
    /// <summary>
    /// The file cannot be processed as the flow configured for the library can not be found
    /// </summary>
    FlowNotFound = 3,
    /// <summary>
    /// THe file was processed, but exited with a failure
    /// </summary>
    ProcessingFailed = 4,
    /// <summary>
    /// The file is a duplicate of an existing library file
    /// </summary>
    Duplicate = 5,
    /// <summary>
    /// The file could not be processed due to a mapping issue
    /// </summary>
    MappingIssue = 6,
    /// <summary>
    /// The library this file was created under no longer exists
    /// </summary>
    MissingLibrary = 7
}

/// <summary>
/// A node/flow part that has been executed
/// </summary>
public class ExecutedNode
{
    /// <summary>
    /// Gets or sets the name of the node part
    /// </summary>
    public string NodeName { get; set; }
    
    /// <summary>
    /// Gets or sets the UID of the node part
    /// </summary>
    public string NodeUid { get; set; }
    
    /// <summary>
    /// Gets or sets the time it took to process this node 
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Gets or sets the output from this node
    /// </summary>
    public int Output { get; set; }
}