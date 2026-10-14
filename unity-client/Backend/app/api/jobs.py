from fastapi import APIRouter, HTTPException

from app.schemas.jobs import JobResponse
from app.services.job_queue import job_store

router = APIRouter(prefix="/jobs")


@router.get("/{job_id}", response_model=JobResponse)
async def get_job(job_id: str) -> JobResponse:
    job = job_store.get_job(job_id)
    if job is None:
        raise HTTPException(status_code=404, detail=f"Job '{job_id}' was not found.")

    return JobResponse(
        jobId=job["jobId"],
        status=job["status"],
        agent=job["agent"],
        result=job["result"],
        artifacts=job["artifacts"],
    )
