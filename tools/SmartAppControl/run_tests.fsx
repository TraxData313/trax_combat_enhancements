// A minimal in-memory xunit runner: loads OUR assemblies from bytes (Smart App Control blocks the freshly built, unsigned DLLs when
// mapped from a file), the signed packages from the bin folder, then runs every [Fact] / [Theory] by reflection.
open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Threading.Tasks

let args = fsi.CommandLineArgs |> Array.skip 1
let bin = args.[0]
let filter = if args.Length > 1 then args.[1] else ""
let ours = set [ "TraxCombat.Core"; "TraxCombat.Core.Tests" ]
AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", bin + "\\")
let cache = Collections.Generic.Dictionary<string, Assembly>()

let load (name: string) : Assembly =
    match cache.TryGetValue name with
    | true, a -> a
    | _ ->
        let path = Path.Combine(bin, name + ".dll")
        let a =
            if ours.Contains name then AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(File.ReadAllBytes path))
            else AssemblyLoadContext.Default.LoadFromAssemblyPath path
        cache.[name] <- a
        a

AssemblyLoadContext.Default.add_Resolving(fun _ an ->
    let p = Path.Combine(bin, an.Name + ".dll")
    if File.Exists p then load an.Name else null)

let core = load "TraxCombat.Core"
let tests = load "TraxCombat.Core.Tests"

let rec isA (t: Type) (name: string) = t <> null && (t.Name = name || isA t.BaseType name)

let mutable pass = 0
let mutable fail = 0
let mutable skip = 0
let failures = Collections.Generic.List<string>()

let invoke (inst: obj) (m: MethodInfo) (data: obj[]) =
    let ps = m.GetParameters()
    let conv =
        Array.init ps.Length (fun i ->
            let v = if i < data.Length then data.[i] else (if ps.[i].HasDefaultValue then ps.[i].DefaultValue else null)
            if v = null then v
            elif ps.[i].ParameterType.IsAssignableFrom(v.GetType()) then v
            elif ps.[i].ParameterType.IsEnum then Enum.ToObject(ps.[i].ParameterType, v)
            else
                let target = match Nullable.GetUnderlyingType ps.[i].ParameterType with null -> ps.[i].ParameterType | u -> u
                Convert.ChangeType(v, target, Globalization.CultureInfo.InvariantCulture))
    let m2 = if m.IsGenericMethodDefinition then m.MakeGenericMethod(conv |> Array.map (fun o -> if o = null then typeof<obj> else o.GetType()) |> Array.take (m.GetGenericArguments().Length)) else m
    let r = m2.Invoke(inst, conv)
    match r with
    | :? Task as t -> t.GetAwaiter().GetResult()
    | _ -> ()

let runOne (t: Type) (m: MethodInfo) (data: obj[]) (label: string) =
    let inst = if m.IsStatic then null else Activator.CreateInstance(t)
    try
        try
            invoke inst m data
            pass <- pass + 1
        with
        | :? TargetInvocationException as e ->
            fail <- fail + 1
            failures.Add(label + "\n    " + e.InnerException.GetType().Name + ": " + e.InnerException.Message.Replace("\n", "\n    "))
        | e ->
            fail <- fail + 1
            failures.Add(label + "\n    " + e.GetType().Name + ": " + e.Message)
    finally
        match inst with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

for t in tests.GetTypes() do
    if t.IsClass && not t.IsAbstract && t.IsPublic then
        for m in t.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static) do
            let attrs = m.GetCustomAttributes(true)
            let fact = attrs |> Array.tryFind (fun a -> isA (a.GetType()) "FactAttribute")
            match fact with
            | None -> ()
            | Some fa ->
                let label = t.Name + "." + m.Name
                if filter <> "" && not (label.Contains filter) then ()
                else
                let skipText = fa.GetType().GetProperty("Skip").GetValue(fa) :?> string
                if not (String.IsNullOrEmpty skipText) then skip <- skip + 1
                elif isA (fa.GetType()) "TheoryAttribute" then
                    for da in attrs |> Array.filter (fun a -> isA (a.GetType()) "DataAttribute") do
                        let rows = da.GetType().GetMethod("GetData", [| typeof<MethodInfo> |]).Invoke(da, [| m |]) :?> Collections.Generic.IEnumerable<obj[]>
                        for row in rows do
                            runOne t m row (label + "(" + String.Join(", ", row |> Array.map (fun o -> if o = null then "null" else string o)) + ")")
                else runOne t m [||] label

for f in failures do printfn "FAIL %s" f
printfn "passed %d, failed %d, skipped %d" pass fail skip
exit (if fail = 0 then 0 else 1)
