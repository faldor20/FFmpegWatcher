
namespace FFmpegWatcher
open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open FSharp.Control;
open FSharp.Control.Reactive
open System
open FFMpegCore

open System.Linq
open System.Linq;
module Watcher =


    ///Returns the filePaths of files not part of the ignorelist
    let checkForNewFiles2 (ignoreList: string[]) (folder) =
       
        DirectoryInfo( folder).GetFiles()|>Array.map(fun i-> i.FullName) |>Array.filter (fun x-> not(ignoreList|>Array.contains x) ) 
        
    

    let ActionNewFiles2 (directory:string)   =
        let event = new Event<string>()
        async{
   
        let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
          
        printfn "{Watcher} Watching : %A"directory
        while true do
            do! Async.Sleep(500);
            let newFilesFunc=
                checkForNewFiles2 
                
            let newFiles=newFilesFunc ignoreList directory   
            for file in newFiles do
                let extension= (Path.GetExtension file)                
                printfn "{Watcher} created scheduling task for file %s" (Path.GetFileName file)
                event.Trigger file
            ignoreList<- ignoreList|> Array.append newFiles
        }|>Async.Start
        event.Publish
        

   
    let GetNewTransfers2 watchDirs =
        let tasks=watchDirs|>List.map (ActionNewFiles2 )
        tasks   
        