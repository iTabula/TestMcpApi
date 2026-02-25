using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController]
public class GeneralController : ControllerBase
{
    [McpServerTool]
    [Description("Explains what KAM is, how it operates as a mortgage and real estate brokerage, and its agent-focused value proposition. " +
        "Use this when the user asks what KAM is, what KAM does, how long KAM has been in business, or why KAM is different. " +
        "Relevant for questions about KAM’s mission, business model, or commission structure.")]
    [HttpGet("/general/what-is-kam")]
    public string GetWhatIsKam()
    {
        return "Welcome to KAM\nKAM Financial & Realty, Inc. (\"KAM\") is the premier mortgage and real estate brokerage dedicated to serving the needs of experienced mortgage and real estate professionals. At KAM, we believe an agent shouldn’t have to give a lot to get a lot. That is why we pay our KAM agents 100% commission splits and provide them all the tools necessary to grow their business and satisfy their clients. So if you are ready to make a change for the better, contact KAM today. You will be happy you did!";
    }

    [McpServerTool]
    [Description("Explains the services, lending programs, and operational support KAM provides for loan officers. " +
        "Use this when the user asks what KAM offers loan officers, which loan types are supported, or what tools are available. " +
        "Relevant for questions about wholesale lender access, loan origination tools, or processing support.")]
    [HttpGet("/general/loan-officer-services")]
    public string GetLoanOfficerServices()
    {
        return "KAM is a licensed mortgage brokerage in California. KAM is approved with several wholesale lenders that do conventional, conforming, FHA, VA, USDA, jumbo, super Jumbo, reverse mortgages, commercial, construction and private money loans. KAM also provides its loan officers with access to credit reports, loan origination software, DU, LP, processing and almost any other need or want a loan officer can think of.";
    }

    [McpServerTool]
    [Description("Explains the Realtor memberships, MLS access, and support services KAM provides for real estate agents. " +
        "Use this when the user asks what KAM does for Realtors, MLS coverage areas, or agent support services. " +
        "Relevant for questions about association memberships, transaction coordination, or marketing support.")]
    [HttpGet("/general/realtor-services")]
    public string GetRealtorServices()
    {
        return "KAM is a licensed Realtor member in California. KAM is a member of the National Association of Realtors, California Association of Realtors, Orange County Associate of Realtors, and San Diego Association of Realtors. KAM’s multiple listing service access includes the Greater Los Angeles Area, Riverside, San Bernardino, Orange County, San Diego County and Imperial County. KAM provides is Realtors with transaction coordinator support, errors and omissions insurance coverage and access to signs and marketing material too. We could go on and on about what we provide our Realtors but we don’t want to bore you.";
    }

    [McpServerTool]
    [Description("Explains the end-to-end KAM workflow from approval and setup through submission, funding, and payment. " +
        "Use this when the user asks how KAM works, what the process is, or what steps are required. " +
        "Highlights licensing prerequisites and the operational milestones for agents and loan officers.")]
    [HttpGet("/general/kam-process")]
    public string GetKamProcess()
    {
        return "- GET APPROVED, Give us your contact info like name, phone and email. Receive Invitation to Join KAM. Complete Paperwork and upload\n" +
               "- SETUP, Transfer your NMLS to KAM, Transfer your real estate license to KAM, Get access to KAM login, Credit Report, Calyx Point, Realtor & MLS, Processor & TC, and Full Lender List\n" +
               "- SUBMIT AND CLOSE, Submit your loan or real estate transaction, Close or fund your real estate or loan transaction, Provide us with the compliance package\n" +
               "- GET PAID, Pickup commission Same Day, Get Commission via wire transfer, direct deposit, courier, or US Mail\n" +
               "- REPEAT AND SMILE, Be Happy you just earned 100%, Smile because you got PAID\n" +
               "- You cannot join KAM unless you are licensed by CA Bureau of Real estate and Licensed by the NMLS";
    }

    [McpServerTool]
    [Description("Explains how agents and loan officers join KAM, including licensing requirements and next steps. " +
        "Use this when the user asks how to join KAM, what is required to join, or how to submit interest. " +
        "Directs the user to complete the join form so a KAM agent can follow up.")]
    [HttpGet("/general/join-kam")]
    public string GetJoinKam()
    {
        return "To Join KAM: You must be both licensed by CA Bureau of Real estate and the NMLS. Please fill out the form below and your information will be sent to a KAM agent.";
    }
}
