window.ffCode = {
    
    initModel: function (variables, sharedScripts) 
    {        
        monaco.languages.typescript.javascriptDefaults.setCompilerOptions({
            target: monaco.languages.typescript.ScriptTarget.ES6,
            allowNonTsExtensions: true
        });
        if(sharedScripts?.length)
        {
            for(let script of sharedScripts)
            {
                let path = script.path.replace('Scripts/', '');
                path = path.replace('.js', '');
                let genCode = `declare module '${path}' { ${script.code} }`;
                monaco.languages.typescript.javascriptDefaults.addExtraLib(
                    genCode,
                    path + '/index.d.ts');
            }
        //     console.log('at end!!!');
        //     monaco.editor.createModel(
        //         "declare class Tester2 { print: function(), multiple: function(any, any), list: function() }",
        //         "javascript"
        //     );
        //     monaco.languages.typescript.javascriptDefaults.addExtraLib(`declare class Car{ 
        //     start()
        //     stop()
        // }`, "Car");
        }
        
        
        monaco.editor.createModel(
            "const Logger = { ILog: function(...any), DLog: function(...any), WLog: function(...any), ELog: function(...any) }",
            "javascript"
        );

        monaco.editor.createModel(
            "const Flow = { " +
            "CreateDirectoryIfNotExists: function (path:string)," +
            "GetDirectorySize: function (path: string):number, " +
            "GetParameter: function (key: string):any, " +
            "MapPath: function (path: string):string, " +
            "MoveFile: function (destination: string), " +
            "ResetWorkingFile: function (), " +
            "SetWorkingFile: function (filename: string, dontDelete: bool), " +
            "SetParameter: function (key:string, value:any)," +
            "NewGuid: function ():string," +
            "CopyToTemp: function(filename:string):string, " + 
            "Execute: function(args:{command:string, arguments: string, argumentList:string[], timeout:number, workingDirectory:string}):{completed:bool, exitCode:number, output:string, standardOutput:string, starndardError:string}," +
            "FileName: string," + 
            "TempPath:string," +            
            "TempPathName:string," + 
            "RunnerUid:string," + 
            "WorkingFile:string," +
            "WorkingFileName:string, " +
            "WorkingFileSize:number," +
            "RelativeFile:string," +
            "IsDirectory:bool," +
            "IsDocker:bool," +
            "IsWindows:bool," +
            "IsLinux:bool," +
            "IsMac:bool," +
            "IsArm:bool," +
            "LibraryPath:string," +
            "}",
            "javascript"
        );
        
        const funFileInfo = `declare function FileInfo(path: string): {
   Exists: bool,
   Length: number,
   Name: string, 
   DirectoryName: string, 
   IsReadOnly: bool,
   CreationTime: date,
   LastWriteTime: date, 
   LastAccessTime: date,
   Extension: string
};`;
        monaco.languages.typescript.javascriptDefaults.addExtraLib(funFileInfo, 'ff.funFileInfo');
        
        monaco.editor.createModel(
            "const Sleep = function(milliseconds)",
            "javascript"
        );

        if (variables) {
            var actualVaraibles = {};
            for (let k in variables) {
                let tk = k;
                let av = actualVaraibles
                while (tk.indexOf('.') > 0) {
                    let nk = tk.substring(0, tk.indexOf('.'));
                    if(!av[nk])
                        av[nk] = {};
                    tk = tk.substring(tk.indexOf('.') + 1);
                    av = av[nk];
                }
                if(!av[tk])
                    av[tk] = variables[k]
            }
            let js = "const Variables = " + JSON.stringify(actualVaraibles);
            monaco.editor.createModel(js, "javascript");
        }
    }
}