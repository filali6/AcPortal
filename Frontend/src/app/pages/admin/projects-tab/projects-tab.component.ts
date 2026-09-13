import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProjectsService } from '../../../core/services/projects.service';
import { LucideAngularModule, ChevronRight } from 'lucide-angular';
import { TabsService } from '../../../core/services/tabs.service';
import { TranslateModule } from '@ngx-translate/core';
@Component({
  selector: 'app-projects-tab',
  standalone: true,
  imports: [CommonModule, LucideAngularModule,TranslateModule],
  templateUrl: './projects-tab.component.html',
  styleUrl: './projects-tab.component.scss'
})
export class ProjectsTabComponent implements OnInit {
  projects: any[] = [];
  selectedProject: any = null;
  loading = false;
  portfolioFilter:string|null=null;
  portfolioFilterName:string|null=null;

  readonly ChevronRight = ChevronRight;

  constructor(private projectsService: ProjectsService,private tabsService:TabsService) {}

  ngOnInit(): void {
    this.load();
    this.tabsService.tabs.subscribe(tabs => {
    const tab = tabs.find(t => t.id === 'projects-tab');
    if (tab?.data?.portfolioFilter) {
      this.portfolioFilter = tab.data.portfolioFilter;
      this.portfolioFilterName = tab.data.portfolioName;
    } else {
      this.portfolioFilter = null;
      this.portfolioFilterName = null;
    }
  });
  }

  load(): void {
    this.loading = true;
    this.projectsService.getAll().subscribe({
        next: (projects) => {
            this.projects = projects;
            this.loading = false;
            // charger le progress pour chaque projet
            projects.forEach(p => {
                this.projectsService.getDetails(p.id).subscribe({
                    next: (details: any) => {
                        const proj = this.projects.find(x => x.id === p.id);
                        if (proj) (proj as any).progress = details.progress;
                    }
                });
            });
        }
    });
}

  selectProject(project: any): void {
    this.loading = true;
    this.projectsService.getDetails(project.id).subscribe({
      next: (d) => {
        this.selectedProject = d;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }
  get filteredProjects(): any[] {
  if (!this.portfolioFilter) return this.projects;
  return this.projects.filter(p => p.portfolioId === this.portfolioFilter);
}
}