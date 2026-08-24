namespace ATS.Data.Repository;

public partial class ATSRepository : IATSRepository
{
	private readonly ATSDBContext _dbcontext;

	public ATSRepository(ATSDBContext dbcontext)
	{
		_dbcontext = dbcontext;
	}
}
