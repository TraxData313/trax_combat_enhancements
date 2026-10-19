// tools/DefaultsTool's "refresh", run in memory (Smart App Control blocks the freshly built DLLs when mapped from a file).
open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text

let args = fsi.CommandLineArgs |> Array.skip 1
let bin = args.[0]          // the test bin folder (Core + Newtonsoft)
let path = args.[1]         // defaults.json
AssemblyLoadContext.Default.add_Resolving(fun _ an ->
    let p = Path.Combine(bin, an.Name + ".dll")
    if File.Exists p then AssemblyLoadContext.Default.LoadFromAssemblyPath p else null)
let core = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(bin, "TraxCombat.Core.dll"))))
let df = core.GetType("TraxCombat.Core.DefaultsFile")
let text = File.ReadAllText(path, Encoding.UTF8)
let check = df.GetMethod("Check").Invoke(null, [| box text |])
let ct = check.GetType()
let valuesOk = ct.GetProperty("ValuesOk").GetValue(check) :?> bool
if not valuesOk then
    printfn "%s" (ct.GetMethod("Describe").Invoke(check, [||]) :?> string)
    exit 1
let diff = ct.GetProperty("CommentsDifference").GetValue(check)
if diff = null then
    printfn "already in step"
    exit 0
let values = ct.GetProperty("Values").GetValue(check)
let w = df.GetMethods() |> Array.find (fun m -> m.Name = "Write" && m.GetParameters().Length = 1)
let output = w.Invoke(null, [| values |]) :?> string
File.WriteAllText(path, output, UTF8Encoding(false))
let again = df.GetMethod("Check").Invoke(null, [| box (File.ReadAllText(path, Encoding.UTF8)) |])
printfn "refreshed; ok = %O" (again.GetType().GetProperty("Ok").GetValue(again))
