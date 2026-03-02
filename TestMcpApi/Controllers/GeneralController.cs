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
        "Use this when the user asks about KAM's identity, operations, history, or differentiation in any format. " +
        "Matches questions like: what is KAM, what KAM is, who is KAM, tell me about KAM, KAM information, " +
        "what does KAM do, what KAM does, KAM services overview, " +
        "how long has KAM been in business, how long KAM has been in business, KAM history, KAM experience, years in business, " +
        "why is KAM different, what makes KAM different, KAM differentiation, why choose KAM, KAM advantages, " +
        "KAM's mission, KAM mission statement, business model, commission structure, commission splits, " +
        "general information about KAM, overview of KAM, describe KAM, explain KAM, " +
        "or any variation in word order, capitalization, or grammar of these questions.")]
    [HttpGet("/general/what-is-kam")]
    public string GetWhatIsKam()
    {
        return "Welcome to KAM \n KAM Financial & Realty, Inc. (\"KAM\") is the premier mortgage and real estate brokerage dedicated to serving the needs of experienced mortgage and real estate professionals. At KAM, we believe an agent shouldn't have to give a lot to get a lot. That is why we pay our KAM agents 100% commission splits and provide them all the tools necessary to grow their business and satisfy their clients. So if you are ready to make a change for the better, contact KAM today. You will be happy you did!";
    }

    [McpServerTool]
    [Description("Explains the services, lending programs, and operational support KAM provides for loan officers. " +
        "Provides detailed information about the loan types supported, wholesale lender access, and tools available to loan officers. " +
        "Use this when the user asks about loan officer services, support, or offerings in any format. " +
        "Matches questions like: what services does KAM provide for loan officers, what does KAM offer loan officers, loan officer services at KAM, " +
        "services for loan officers, KAM loan officer support, what can loan officers do at KAM, " +
        "which loan types are supported, what loan types can I do at KAM, available loan types, loan programs at KAM, " +
        "what tools are available for loan officers, loan officer tools, LO tools, origination tools, " +
        "wholesale lender access, lenders available, which lenders does KAM work with, lender list, " +
        "do you support FHA loans, VA loans, conventional loans, jumbo loans, reverse mortgages, commercial loans, " +
        "credit reports, loan origination software, DU access, LP access, processing support, " +
        "or any variation in word order, capitalization, or grammar of these questions.")]
    [HttpGet("/general/loan-officer-services")]
    public string GetLoanOfficerServices()
    {
        return "KAM is a licensed mortgage brokerage in California. KAM is approved with several wholesale lenders that do conventional, conforming, FHA, VA, USDA, jumbo, super Jumbo, reverse mortgages, commercial, construction and private money loans. KAM also provides its loan officers with access to credit reports, loan origination software, DU, LP, processing and almost any other need or want a loan officer can think of.";
    }

    [McpServerTool]
    [Description("Explains the Realtor memberships, MLS access, and support services KAM provides for real estate agents. " +
        "Provides detailed information about association memberships, MLS coverage areas, and operational support available to Realtors. " +
        "Use this when the user asks about realtor services, real estate agent support, or MLS access in any format. " +
        "Matches questions like: what does KAM do for realtors, what services does KAM provide for realtors, realtor services at KAM, " +
        "services for realtors, services for real estate agents, KAM realtor support, what can realtors do at KAM, " +
        "MLS coverage areas, what are the MLS areas KAM covers, which MLS does KAM have access to, MLS access, available MLS regions, " +
        "what support is available for agents, agent support services, realtor tools, real estate agent tools, " +
        "association memberships, which associations is KAM a member of, NAR membership, CAR membership, local association access, " +
        "transaction coordinator support, TC support, E&O insurance, errors and omissions coverage, " +
        "signs and marketing material, marketing support, realtor resources, " +
        "or any variation in word order, capitalization, or grammar of these questions.")]
    [HttpGet("/general/realtor-services")]
    public string GetRealtorServices()
    {
        return "KAM is a licensed Realtor member in California. KAM is a member of the National Association of Realtors, California Association of Realtors, Orange County Associate of Realtors, and San Diego Association of Realtors. KAM's multiple listing service access includes the Greater Los Angeles Area, Riverside, San Bernardino, Orange County, San Diego County and Imperial County. KAM provides is Realtors with transaction coordinator support, errors and omissions insurance coverage and access to signs and marketing material too. We could go on and on about what we provide our Realtors but we don't want to bore you.";
    }

    [McpServerTool]
    [Description("Explains the end-to-end KAM workflow from approval and setup through submission, funding, and payment. " +
        "Use this when the user asks about how KAM works, the process, workflow, or steps in any format. " +
        "Matches questions like: how does KAM work, how KAM works, explain the KAM process, " +
        "what is the KAM process, what do I need to know about the kam process, KAM workflow, " +
        "what is the process, what are the steps, how do I get started with KAM, getting started at KAM, " +
        "what steps are required, process steps, workflow steps, KAM procedures, " +
        "how do I get paid, payment process, commission payment, " +
        "what are the requirements, licensing prerequisites, what licenses do I need, " +
        "onboarding process, setup process, submission process, closing process, " +
        "or any variation in word order, capitalization, or grammar of these questions. " +
        "Also highlights licensing prerequisites and operational milestones for agents and loan officers.")]
    [HttpGet("/general/kam-process")]
    public string GetKamProcess()
    {
        return "The KAM Process involves:" +
               "1- GET APPROVED, Give us your contact info like name, phone and email. Receive Invitation to Join KAM. Complete Paperwork and upload.\n" +
               "2- SETUP, Transfer your NMLS to KAM, Transfer your real estate license to KAM, Get access to KAM login, Credit Report, Calyx Point, Realtor & MLS, Processor & TC, and Full Lender List.\n" +
               "3- SUBMIT AND CLOSE, Submit your loan or real estate transaction, Close or fund your real estate or loan transaction, Provide us with the compliance package.\n" +
               "4- GET PAID, Pickup commission Same Day, Get Commission via wire transfer, direct deposit, courier, or US Mail.\n" +
               "5- REPEAT AND SMILE, Be Happy you just earned 100%, Smile because you got PAID.\n" +
               "Please note, You cannot join KAM unless you are licensed by CA Bureau of Real estate and Licensed by the NMLS";
    }

    [McpServerTool]
    [Description("Explains how agents and loan officers join KAM, including licensing requirements and next steps. " +
        "Provides information about the requirements to become a KAM agent or loan officer and directs users to complete the join form. " +
        "Use this when the user asks about joining, applying, signing up, or becoming a KAM agent in any format. " +
        "Matches questions like: how to join KAM, how do I join KAM, join KAM, joining KAM, " +
        "how to become a KAM agent, how do I become a KAM agent, become an agent, becoming a KAM agent, " +
        "what is required to join, what do I need to join KAM, join requirements, joining requirements, " +
        "how do I apply to KAM, apply to KAM, application process, applying to KAM, " +
        "what are the requirements to join, requirements for joining, prerequisites to join, " +
        "how to submit interest, express interest in KAM, contact KAM about joining, " +
        "sign up with KAM, signup process, registration process, " +
        "licensing requirements, what licenses do I need to join, NMLS requirement, BRE requirement, " +
        "or any variation in word order, capitalization, or grammar of these questions.")]
    [HttpGet("/general/join-kam")]
    public string GetJoinKam()
    {
        return "To Join KAM: You must be both licensed by CA Bureau of Real estate and the NMLS. Please fill out the form below and your information will be sent to a KAM agent.";
    }
}
