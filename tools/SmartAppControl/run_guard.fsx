// tools/AssemblyGuard run IN MEMORY (Smart App Control refuses its freshly built exe / dll when mapped from a file).
open System
open System.IO
open System.Runtime.Loader
let args = fsi.CommandLineArgs |> Array.skip 1
let bin = args.[0]
AssemblyLoadContext.Default.add_Resolving(fun _ an ->
    let p = Path.Combine(bin, an.Name + ".dll")
    if File.Exists p then AssemblyLoadContext.Default.LoadFromAssemblyPath p else null)
let guard = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(bin, "AssemblyGuard.dll"))))
let rest = args |> Array.skip 1
let code = guard.EntryPoint.Invoke(null, [| box rest |])
printfn "AssemblyGuard exit code %O" code
exit (match code with :? int as c -> c | _ -> 0)
