import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectsService } from '../../../core/services/projects.service';
import { ToastService } from '../../../core/services/toast.service';
import { LucideAngularModule, ChevronRight, Edit2, X } from 'lucide-angular';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { TabsService } from '../../../core/services/tabs.service';
import { UtilsService } from '../../../core/services/utils.service';
import { ModalComponent } from '../../../core/components/modal/modal.component';
@Component({
  selector: 'app-portfolios-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule,ModalComponent],
  templateUrl: './portfolios-tab.component.html',
  styleUrl: './portfolios-tab.component.scss'
})
export class PortfoliosTabComponent implements OnInit {
  @Input() directors: any[] = [];

  portfolios: any[] = [];
  selectedPortfolio: any = null;
  loading = false;

  // Modal
  showModal = false;
  modalMinimized = false;
  modalPortfolio: any = null;
  selectedDirectorId = '';


  

  readonly ChevronRight = ChevronRight;
  readonly Edit2 = Edit2;
  readonly X = X;

  constructor(
    private projectsService: ProjectsService,
    private toastService: ToastService,
    private http: HttpClient,
    private tabsService: TabsService,
    public utils:UtilsService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.projectsService.getAllPortfolios().subscribe({
      next: (p) => this.portfolios = p
    });
  }

  selectPortfolio(portfolio: any): void {
  
  this.tabsService.openTab({
    id: 'projects-tab',
    title: 'Projects',
    type: 'create-project',
    data: { portfolioFilter: portfolio.id, portfolioName: portfolio.name }
  });
}

  openModal(portfolio: any): void {
    this.modalPortfolio = portfolio;
    this.selectedDirectorId = portfolio.director?.id || '';
    this.showModal = true;
      
  }

  closeModal(): void {
    this.showModal = false;
    this.modalPortfolio = null;
    this.selectedDirectorId = '';
  }

  changeDirector(): void {
    if (!this.selectedDirectorId || !this.modalPortfolio) return;
    this.loading = true;
    this.projectsService.assignDirectorToPortfolio(this.modalPortfolio.id, this.selectedDirectorId).subscribe({
      next: () => {
        this.toastService.show('Director updated!', 'success');
        this.closeModal();
        this.loading = false;
        this.load();
      },
      error: () => {
        this.toastService.show('Error updating director', 'error');
        this.loading = false;
      }
    });
  }
}