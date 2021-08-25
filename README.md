# Sample Project

1. Web - Hosts main site.  Including the 'Button'.  Also queries data from DynamoDB.
2. IngressLambda - Clicking the 'Button' calls this Api Gateway + Lambda function.  Puts data on processing SQS.
3. ProcessingLambda - listens to SQS.  Puts data into DynamoDB
4. CDK - Deploys all the things.  Also seeds DynamoDB with some sample data.



#### Notes

becareful live coding 
---- have a backup solution
--- precreate projects and then just 'add them' to solution.  helps avoid risk of Visual Studio being slow.
-- use snippets, so can talk about it, but not type everything

Business Cases:
- Credit application / Load Application / Car Insurance.

----- **CloudAutoInsurance**
 
 ==== TV commercial plays during sportnig event.  Tells customers to go to web page to get quote.


 #### Talk Outline

 1. Introduce speakers

 1. Problem Space
    Our fictional insurance company CloudAuto, Aired a TV commercial encouraging users to come to a website to receive a basic quote.  We need to build the basic infrastructure for that campaign.

    1. Web Site to host the landing page
    1. Api to receive quote request.  Performs basic validation logic
    1. Backend processor to generate 
    1. Web Site to host an admin screen.

    We'll create a single Asp.Net MVC application for both the customer facing portion and admin screen and we'll host it in Elastic Beanstalk, AWS's managed Web Hosting.
    The quote ingestion API is expected to be quite bursty, so we'll use '

    This talk is primarily about the CDK.  There's a lot of options on AWS and this talk isn't about recommending a specific set of resources for this problem, so we're not endorsing Beanstalk over Fargate for example.

