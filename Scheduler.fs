namespace FFmpegWatcher
open System
open System.IO

module Scheduler=
    type Availability=
        |Available=0
        |Deleted=1
        |Cancelled=2
    let isAvailable (source:string)  =
        async {
            let fileName= Path.GetFileName (string source)
            let mutable currentFile = new FileInfo(source)
            let mutable loop = true
            let mutable availability=Availability.Available
            while loop do
                try
                    using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                        stream.Close())
                    availability<-Availability.Available
                    loop<-false
                with 
                    | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                        printfn "%s deleted while waiting to be available" fileName
                        availability<-Availability.Deleted
                        loop<-false
                    | :? IOException ->
                        
                        do! Async.Sleep(1000)

                    | ex  ->
                        printfn "file failed with %A" ex.Message
                        loop<- false
                        availability<-Availability.Deleted
            return availability
        }
    let runOnceAvailable (task:string->Async<unit>) file =
        printfn "waiting on %s to be available" file
        let available=isAvailable file |>Async.RunSynchronously
        printfn "Availabel and astarting task %s" file
        match available with
        |Availability.Available->
            task file |>Async.RunSynchronously     
        |_-> printfn "file was removed before it could be run"