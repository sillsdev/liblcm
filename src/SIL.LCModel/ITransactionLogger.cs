namespace SIL.LCModel
{
   internal interface ITransactionLogger
   {
	   void AddBreadCrumb(string description);
   }
}
