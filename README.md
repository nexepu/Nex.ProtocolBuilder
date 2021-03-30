# Nex.ProtocolBuilder
A protocol builder for Dofus 2.0 that was based of cookie's old protocol builder that i updated and upgraded throughout the years.

This tool turns the packets used by Dofus 2 from ActionScript to C# so you could use them in your own program if you intend to connect with it to Dofus's servers.

This tool is easily modifiable for your needs, check TypeMessageGenerator.cs and DatacenterGenerator.cs.



USAGE:

Decompile the sources of the DofusInvoker.swf that are in this path : /scripts/com/ankamagames/dofus/network/ or /scripts/com/ankamagames/dofus/datacenter if you intend to build the datacenter instead of the network using JPEX decompiler,
if you use another decompiler, it might not work.
Put the decompiled sources in the /Input folder and run the program and tadaaa, the C# sources will be located in the Output folder.

Also, don't forget to change the namespaces in Program.cs to your own tool's namespaces.
