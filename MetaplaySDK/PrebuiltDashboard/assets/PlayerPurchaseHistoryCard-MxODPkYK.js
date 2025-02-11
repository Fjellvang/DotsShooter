import{d as j,z as k,c as R,M as U,o,a as u,F as z,r as K,e as m,w as n,p as a,t as l,a2 as ee,n as I,_ as Y,a6 as B,x as T,f as b,C as F,ad as G,L as te,s as ne,v as re,q as se,ae as H,b as x,k as d,l as h,$ as ae}from"./index-B7jvAjdE.js";import{M as oe}from"./MActionModalButton-CvWVuTB-.js";import{M,a as v,b as D,c as p}from"./metaListUtils-B-xQ0HnD.js";import{a as N}from"./players-CydHenLG.js";import{a as q}from"./rewardUtils-BIwy3Xgi.js";import"./MActionModal-coQLkvWs.js";import"./index-fVjXqxSF.js";const ie=j({__name:"OfferSpan",props:{offer:{type:Object,required:!0}},setup(g,{expose:t}){t();const c=g,e=k(),y=R(()=>c.offer.$type==="Metaplay.Core.InAppPurchase.ResolvedPurchaseMetaRewards"?c.offer.rewards.map(i=>q(i).getDisplayValue(i)):e.gameSpecific.iapContents.find(i=>i.$type===c.offer.$type)?.getDisplayContent(c.offer)??null),_={props:c,coreStore:e,displayStrings:y,computed:R,get rewardWithMetaData(){return q},get MBadge(){return U},get useCoreStore(){return k}};return Object.defineProperty(_,"__isScriptSetup",{enumerable:!1,value:!0}),_}}),de={key:0},ue={key:1},le={class:"text-danger"};function ce(g,t,c,e,y,_){return e.displayStrings?(o(),u("span",de,[(o(!0),u(z,null,K(e.displayStrings,i=>(o(),m(e.MBadge,{class:"tw-ml-1",key:i},{default:n(()=>[a(l(i),1)],void 0,!0),_:2},1024))),128))])):(o(),u("span",ue,[ee(g.$slots,"default",{},()=>[I("span",le,"Error: undefined offer type '"+l(c.offer.$type)+"'",1)])]))}const fe=Y(ie,[["render",ce],["__file","OfferSpan.vue"]]),pe=j({__name:"PlayerPurchaseHistoryCard",props:{playerId:{}},setup(g,{expose:t}){t();const c=g,e=G(),y=k(),_=B(),{data:i,refresh:w}=T(N(c.playerId)),f=b(),P=b({}),r=["platform","productId","transactionId","orderId"],C=[new M("Purchase time","purchaseTime",v.Ascending),new M("Purchase time","purchaseTime",v.Descending),new M("Price","referencePrice",v.Ascending),new M("Price","referencePrice",v.Descending)],L=b(1),E=R(()=>[D.asDynamicFilterSet(f.value,"product-id",s=>s.productId),new D("status",[new p("Valid receipt",s=>s.status==="ValidReceipt"),new p("Invalid receipt",s=>s.status==="InvalidReceipt"),new p("Pending validation",s=>s.status==="PendingValidation"),new p("Refunded",s=>s.status==="Refunded"),new p("Receipt already used",s=>s.status==="ReceiptAlreadyUsed"),new p("Other",s=>!["ValidReceipt","PendingValidation","Refunded","ReceiptAlreadyUsed","InvalidReceipt"].includes(s.status))])]);F(i,A,{immediate:!0});function A(){if(f.value=[],i.value){for(const s of i.value.model.inAppPurchaseHistory)s.isPending=!1,f.value.unshift(s);for(const s in i.value.model.pendingInAppPurchases)i.value.model.pendingInAppPurchases[s].isPending=!0,f.value.unshift(i.value.model.pendingInAppPurchases[s]);for(const s of i.value.model.duplicateInAppPurchaseHistory)s.isDuplicate=!0,f.value.push(s);for(const s in i.value.model.failedInAppPurchaseHistory)f.value.push(i.value.model.failedInAppPurchaseHistory[s])}}const{showSuccessNotification:O}=H();async function W(){await e.post(`/players/${i.value.id}/refund/${P.value.transactionId}`),O("Refund requested successfully."),w()}function Q(){P.value={}}function J(s){const S={PendingValidation:{text:"Pending validation",statusClass:"text-warning",badgeVariant:"neutral"},ValidReceipt:{text:"Validated",statusClass:"text-success",badgeVariant:"success"},InvalidReceipt:{text:"Invalid Receipt",statusClass:"text-danger",badgeVariant:"danger"},ReceiptAlreadyUsed:{text:"Receipt already used",statusClass:"text-warning",badgeVariant:"neutral"},Refunded:{text:"Refunded",statusClass:"text-warning",badgeVariant:"neutral"}};return s.status in S?S[s.status]:{text:s.status,statusClass:"text-warning",badgeVariant:"neutral"}}function X(s){const S=y.gameSpecific.iapContents.find($=>$.$type===s.dynamicContent.$type);return S?S.getDisplayContent(s.dynamicContent).join(", "):null}function Z(s){return s.transactionId}const V={props:c,gameServerApi:e,coreStore:y,staticInfos:_,playerData:i,playerRefresh:w,displayedHistory:f,iapToRefund:P,searchFields:r,sortOptions:C,defaultSortOption:L,filterSets:E,updateDisplayedHistory:A,showSuccessNotification:O,refund:W,resetModal:Q,validationStatusInfo:J,tryGetDynamicContentString:X,getItemKey:Z,computed:R,ref:b,watch:F,get useGameServerApi(){return G},get useStaticInfos(){return B},get MetaListFilterOption(){return p},get MetaListFilterSet(){return D},get MetaListSortDirection(){return v},get MetaListSortOption(){return M},get MTooltip(){return te},get MBadge(){return U},get MList(){return ne},get MListItem(){return re},get MCollapse(){return se},get MActionModalButton(){return oe},get useNotifications(){return H},get useSubscription(){return T},get useCoreStore(){return k},get getSinglePlayerSubscriptionOptions(){return N},OfferSpan:fe};return Object.defineProperty(V,"__isScriptSetup",{enumerable:!1,value:!0}),V}}),me={key:0},ge={class:"tw-mr-1"},ye={key:0,class:"tw-text-neutral-500"},_e={key:1},he={class:"text-muted"},Ie={key:0,class:"text-muted"},Pe={key:1},Se={key:0,class:"text-muted"},Me={key:1},ve={key:2},we={key:0,class:"text-muted"},Ce={key:1},be={key:0},xe={key:1},ke={key:0},Re={key:0},Le={key:1};function De(g,t,c,e,y,_){const i=x("meta-time"),w=x("meta-reward-badge"),f=x("meta-no-seatbelts"),P=x("meta-list-card");return e.playerData?(o(),u("div",me,[d(P,{class:"tw-mb-4",title:"Purchase History",icon:"money-check-alt",itemList:e.displayedHistory,getItemKey:e.getItemKey,searchFields:e.searchFields,sortOptions:e.sortOptions,defaultSortOption:e.defaultSortOption,filterSets:e.filterSets,emptyMessage:"No purchases... yet!","data-testid":"player-purchase-history-card"},{"item-card":n(({item:r})=>[d(e.MCollapse,{extraMListItemMargin:""},{header:n(()=>[d(e.MListItem,{noLeftPadding:""},{badge:n(()=>[r.isPending?(o(),m(e.MBadge,{key:0,variant:"warning"},{default:n(()=>t[2]||(t[2]=[a("Pending")]),void 0,!0),_:1})):r.isDuplicate?(o(),m(e.MBadge,{key:1},{default:n(()=>t[3]||(t[3]=[a("Duplicate")]),void 0,!0),_:1})):(o(),m(e.MBadge,{key:2,variant:e.validationStatusInfo(r).badgeVariant},{default:n(()=>[a(l(e.validationStatusInfo(r).text),1)],void 0,!0),_:2},1032,["variant"]))]),"top-right":n(()=>[d(i,{date:r.purchaseTime,showAs:"date"},null,8,["date"])]),"bottom-left":n(()=>[a(l(r.transactionId),1)]),"bottom-right":n(()=>[a(l(r.platform),1)]),default:n(()=>[I("span",ge,l(r.productId),1),r.subscriptionQueryResult===null?(o(),u("span",ye,"($"+l(r.referencePrice.toFixed(2))+")",1)):(o(),u("span",_e,[t[0]||(t[0]=a("(")),d(e.MTooltip,{content:"Subscription purchases are not applied directly towards the player's total spend. Please see Subscription History instead."},{default:n(()=>[a("$"+l(r.referencePrice.toFixed(2)),1)],void 0,!0),_:2},1024),t[1]||(t[1]=a(")"))]))],void 0,!0),_:2},1024)]),default:n(()=>[d(e.MList,{showBorder:"",striped:""},{default:n(()=>[d(e.MListItem,{condensed:""},{"top-right":n(()=>[a(l(r.platform),1)]),default:n(()=>[t[4]||(t[4]=a("Platform"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[I("span",he,l(r.transactionId),1)]),default:n(()=>[t[5]||(t[5]=a("Transaction ID"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[r.orderId===null?(o(),u("span",Ie,"None")):(o(),u("span",Pe,l(r.orderId),1))]),default:n(()=>[t[6]||(t[6]=a("Order ID"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[a(l(r.productId),1)]),default:n(()=>[t[7]||(t[7]=a("Product ID"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[a(l(r.platformProductId),1)]),default:n(()=>[t[8]||(t[8]=a("Platform Product ID"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[a("$"+l(r.referencePrice.toFixed(2)),1)]),default:n(()=>[t[9]||(t[9]=a("Reference Price"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[r.dynamicContent?e.tryGetDynamicContentString(r)?(o(),u("span",Me,l(e.tryGetDynamicContentString(r)),1)):(o(),u("span",ve,"Unknown "+l(r.dynamicContent.$type),1)):(o(),u("span",Se,"None"))]),default:n(()=>[t[10]||(t[10]=a("Related Offer"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[!r.resolvedContent&&!r.resolvedDynamicContent?(o(),u("span",we,"None")):(o(),u("span",Ce,[r.resolvedContent?(o(),u("span",be,[d(e.OfferSpan,{offer:r.resolvedContent},null,8,["offer"])])):h("",!0),r.resolvedDynamicContent?(o(),u("span",xe,[(o(!0),u(z,null,K(r.resolvedDynamicContent.rewards,(C,L)=>(o(),m(w,{key:L,reward:C},null,8,["reward"]))),128))])):h("",!0)]))]),default:n(()=>[t[11]||(t[11]=a("Content"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[d(i,{date:r.purchaseTime,showAs:"datetime"},null,8,["date"])]),default:n(()=>[t[12]||(t[12]=a("Purchase Time"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[d(i,{date:r.claimTime,showAs:"datetime"},null,8,["date"])]),default:n(()=>[t[13]||(t[13]=a("Claim Time"))],void 0,!0),_:2},1024),d(e.MListItem,{condensed:""},{"top-right":n(()=>[I("div",{class:ae(["text-right text-break-word",e.validationStatusInfo(r).statusClass])},l(e.validationStatusInfo(r).text),3)]),default:n(()=>[t[14]||(t[14]=a("Status"))],void 0,!0),_:2},1024),r.platform=="Google"&&!r.isPending&&r.status!="Refunded"?(o(),m(e.MListItem,{key:0,condensed:""},{"top-right":n(()=>[e.staticInfos.featureFlags.googlePlayInAppPurchaseRefunds?(o(),m(e.MActionModalButton,{key:0,"modal-title":"Refund In-App-Purchase",action:e.refund,"trigger-button-label":"Google Play Refund","trigger-button-size":"small",variant:"danger","ok-button-label":"Refund",permission:"api.players.iap_refund",onShow:C=>e.iapToRefund=r},{default:n(()=>[I("p",null,[t[15]||(t[15]=a("You can request a refund for the Google Play purchase ")),d(e.MBadge,null,{default:n(()=>[a(l(e.iapToRefund.transactionId),1)],void 0,!0),_:1}),t[16]||(t[16]=a(". The refund result will be visible in the Google Play Publisher console."))]),t[17]||(t[17]=I("span",{class:"tw-text-xs+ tw-text-neutral-500"},"Note: You cannot see refund status here because the Google Play API does not return the result to us. Requesting a refund multiple times from here will not issue the refund more than once.",-1)),d(f,{class:"tw-mt-4"})],void 0,!0),_:2},1032,["onShow"])):h("",!0)]),default:n(()=>[e.staticInfos.featureFlags.googlePlayInAppPurchaseRefunds?h("",!0):(o(),u("div",ke,[r.isPending?(o(),u("span",Le,"You can only refund valid Google Play purchases.")):(o(),u("span",Re,"Google Play refunds not configured."))]))],void 0,!0),_:2},1024)):h("",!0)],void 0,!0),_:2},1024)],void 0,!0),_:2},1024)]),_:1},8,["itemList","defaultSortOption","filterSets"])])):h("",!0)}const He=Y(pe,[["render",De],["__file","PlayerPurchaseHistoryCard.vue"]]);export{He as default};
