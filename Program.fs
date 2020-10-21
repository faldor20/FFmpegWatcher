// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
namespace FFmpegWatcher
open System
open System.IO
open FFMpegCore
open FSharp.Control.Reactive
open Legivel.Serialization
type WatchDir={Source:string;Dest:string; Args:string option}
type YamlConfig={
    TranscodeExts:string list
    DefaultArgs:string
    WatchDirs:WatchDir list
}

module Main=
    ///calls ffmpeg with the supplied args
    let ffmpegConvert (outpath:string) args (path:string)=
        async{
            let fileName= Path.GetFileName path
            
            let outFile=Path.ChangeExtension( (outpath+fileName),".mp4")
            printfn "putting file in  %s with args %s " outFile  args
            let res=
                FFMpegArguments
                    .FromInputFiles([|FileInfo(path) |])
                    .WithCustomArgument(args)
                    .OutputToFile(outFile ,true)
                    .ProcessSynchronously()

            match res with
            |true-> printfn "sucessfully transcoded %s"  fileName
            |false->printfn "failed transcoding %s" fileName
        }
    ///Used mostly for making sure path names have a / at the end
    let normalizeData (config:YamlConfig)=
        let dirs =config.WatchDirs|>List.map(fun x-> 
           let dest= x.Dest.TrimEnd('/')+"/"
           let source= x.Source.TrimEnd('/')+"/"
           printfn "%A" dest
           {x with Source=source;Dest= dest})
        {config with WatchDirs=dirs}; 
    ///reads the Config.yaml file 
    let readConfig()=
        let configPath="Config.yaml"
        let mutable configText=File.ReadAllText(configPath)
        (* try
            
        with
        |_-> printfn "config file at %s could not be read" configPath *)
        match Deserialize<YamlConfig>(configText) with
        |head::_ ->
            match head with
            |Success s->Some  (normalizeData s.Data)
            |e->
                printfn "%A"e
                None
        |[]-> 
            printfn "couldn't pars yaml, Config file empty"
            None
    
    let runProgram (config:YamlConfig)=
        printfn "Ready to transcode files with extnesions:%A" config.TranscodeExts
        let jobStreams=
            config.WatchDirs|>List.map( fun watch->
                let args=
                    match watch.Args with 
                    |Some arg-> arg
                    |None-> config.DefaultArgs
                let conv= ffmpegConvert watch.Dest args
                (conv,(Watcher.getNewFilesForDir watch.Source):>IObservable<string>)
            )
        let streams=
            jobStreams|>List.map(fun (task,stream)->
                stream.Subscribe(fun file->
                    printfn "waiting"
                    if config.TranscodeExts |>List.contains (Path.GetExtension file) then
                        try
                            Scheduler.waitTillavailable task file
                        with|e-> printfn "ERROR: something went wrong processing file %s %A" file e
                    else
                        printfn "WARNING: Found file '%s' that didn't match accepted extensions"( Path.GetFileName file)
                )|>ignore
                stream
            )|>List.toArray
        Observable.wait( Observable.mergeArray streams)

        
    [<EntryPoint>]
    let main argv =
        FFMpegOptions.Configure(FFMpegOptions ( RootDirectory = "./ffmpeg", TempDirectory = "/tmp" ));
        let config= readConfig()
        match config with 
        |Some cfg -> runProgram cfg |>ignore
        |None ->printfn " config file reading failed. no point continuing."
        
        
        printfn "finished somehow"
        0